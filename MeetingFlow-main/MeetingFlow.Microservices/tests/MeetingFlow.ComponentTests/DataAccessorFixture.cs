using DataAccessor.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MeetingFlow.ComponentTests;

// Spins up a real, throwaway Postgres instance for the whole test class,
// mirroring what DataAccessor's own Program.cs does with UseNpgsql + EnsureCreated,
// so the FK constraints EF Core generates from OnModelCreating are genuinely enforced.
public class DataAccessorFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public MeetingFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeetingFlowDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new MeetingFlowDbContext(options);
    }
}
