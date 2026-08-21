using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using NotificationsAccessor.Data;
using NotificationsAccessor.Infrastructure;
using NotificationsAccessor.Messaging;
using RegistrationsManager.Messaging;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace MeetingFlow.IntegrationTests;

// Boundary under test: RegistrationsManager's real EventPublisher publishing to a real
// RabbitMQ broker, consumed by NotificationsAccessor's real RegistrationEventConsumer.
// Neither class is reimplemented or faked here — the whole point is proving the two
// independently-hardcoded exchange/routing-key/queue declarations actually agree, and
// that the shared RegistrationCreatedV1 contract serializes/deserializes correctly
// across the wire. Nothing else in the system (Gateway, Managers' HTTP layer, Scheduling
// Engine) is started.
public class RegistrationEventIntegrationFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();

    RegistrationEventConsumer? _consumer;
    ServiceProvider? _notificationsServices;

    public EventPublisher Publisher { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        var services = new ServiceCollection();
        services.AddDbContext<NotificationsDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        _notificationsServices = services.BuildServiceProvider();

        await using (var db = _notificationsServices.GetRequiredService<NotificationsDbContext>())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RABBITMQ_URL"] = _rabbitMq.GetConnectionString()
            })
            .Build();

        _consumer = new RegistrationEventConsumer(
            _notificationsServices.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<RegistrationEventConsumer>.Instance,
            new FakeSmtpGateway(NullLogger<FakeSmtpGateway>.Instance));
        await _consumer.StartAsync(CancellationToken.None);

        // Give the consumer's background loop time to connect, declare, and bind
        // before the first test publishes — otherwise an early publish could be lost.
        await Task.Delay(1000);

        Publisher = await EventPublisher.CreateAsync(_rabbitMq.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_consumer is not null) await _consumer.StopAsync(CancellationToken.None);
        await Publisher.DisposeAsync();
        _notificationsServices?.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }

    public NotificationsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);
}
