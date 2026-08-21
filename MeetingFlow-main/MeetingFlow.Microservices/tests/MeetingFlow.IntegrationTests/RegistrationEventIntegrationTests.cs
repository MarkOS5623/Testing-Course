using MeetingFlow.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace MeetingFlow.IntegrationTests;

public class RegistrationEventIntegrationTests : IClassFixture<RegistrationEventIntegrationFixture>
{
    readonly RegistrationEventIntegrationFixture _fixture;

    public RegistrationEventIntegrationTests(RegistrationEventIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Event_published_by_RegistrationsManager_is_consumed_by_NotificationsAccessor_over_real_RabbitMQ()
    {
        var attendeeId = Guid.NewGuid();
        var evt = new RegistrationCreatedV1(
            EventId: Guid.NewGuid(),
            RegistrationId: Guid.NewGuid(),
            MeetingId: Guid.NewGuid(),
            AttendeeId: attendeeId,
            MeetingTitle: "Integration Boundary Conference",
            RecipientName: "Ada Lovelace",
            RecipientEmail: "ada@test.example",
            RegisteredAt: DateTimeOffset.UtcNow);

        await _fixture.Publisher.PublishAsync("registration.created.v1", evt);

        var notification = await PollForNotificationAsync(attendeeId, timeout: TimeSpan.FromSeconds(15));

        Assert.NotNull(notification);
        Assert.Equal("Registration confirmed: Integration Boundary Conference", notification.Subject);
        Assert.Contains(evt.RegistrationId.ToString(), notification.Body);
    }

    async Task<NotificationsAccessor.Models.Notification?> PollForNotificationAsync(Guid attendeeId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = _fixture.CreateDbContext();
            var found = await db.Notifications.FirstOrDefaultAsync(n => n.AttendeeId == attendeeId);
            if (found is not null) return found;

            await Task.Delay(250);
        }

        return null;
    }
}
