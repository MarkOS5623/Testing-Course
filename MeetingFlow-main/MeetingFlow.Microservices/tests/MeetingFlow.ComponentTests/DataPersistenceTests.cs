using DataAccessor.Models;
using DataAccessor.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MeetingFlow.ComponentTests;

public class DataPersistenceTests : IClassFixture<DataAccessorFixture>
{
    readonly DataAccessorFixture _fixture;

    public DataPersistenceTests(DataAccessorFixture fixture) => _fixture = fixture;

    async Task<(Guid VenueId, Guid MeetingId)> CreateVenueAndMeetingAsync()
    {
        var venueId = Guid.NewGuid();
        await using (var db = _fixture.CreateDbContext())
        {
            await new MeetingsRepository(db).CreateVenueAsync(new Venue
            {
                Id = venueId,
                Name = "Test Venue",
                Address = "1 Test St",
                City = "Testville",
                Capacity = 100
            });
        }

        var meetingId = Guid.NewGuid();
        await using (var db = _fixture.CreateDbContext())
        {
            await new MeetingsRepository(db).CreateMeetingAsync(new Meeting
            {
                Id = meetingId,
                Title = "Test Meeting",
                Description = "A test meeting",
                Status = "Published",
                StartsAt = DateTimeOffset.UtcNow.AddDays(1),
                EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
                CreatedAt = DateTimeOffset.UtcNow,
                VenueId = venueId
            });
        }

        return (venueId, meetingId);
    }

    [Fact]
    public async Task CreateVenueAsync_persists_the_venue()
    {
        var venueId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            await new MeetingsRepository(db).CreateVenueAsync(new Venue
            {
                Id = venueId,
                Name = "Persisted Venue",
                Address = "1 Persist St",
                City = "Persistville",
                Capacity = 50
            });
        }

        await using var verifyDb = _fixture.CreateDbContext();
        var saved = await verifyDb.Venues.FindAsync(venueId);

        Assert.NotNull(saved);
        Assert.Equal("Persisted Venue", saved.Name);
        Assert.Equal(50, saved.Capacity);
    }

    [Fact]
    public async Task CreateMeetingAsync_with_existing_venue_returns_meeting_with_venue_loaded()
    {
        var (venueId, meetingId) = await CreateVenueAndMeetingAsync();

        await using var db = _fixture.CreateDbContext();
        var meeting = await new MeetingsRepository(db).GetByIdAsync(meetingId);

        Assert.NotNull(meeting);
        Assert.Equal("Test Meeting", meeting.Title);
        Assert.NotNull(meeting.Venue);
        Assert.Equal(venueId, meeting.Venue.Id);
        Assert.Equal("Test Venue", meeting.Venue.Name);
    }

    [Fact]
    public async Task GetAllAsync_GetByIdAsync_and_GetRegistrationContextAsync_return_the_created_meeting_with_venue_loaded()
    {
        var (venueId, meetingId) = await CreateVenueAndMeetingAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var all = await new MeetingsRepository(db).GetAllAsync();
            var found = Assert.Single(all, m => m.Id == meetingId);
            Assert.Equal(venueId, found.Venue.Id);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var byId = await new MeetingsRepository(db).GetByIdAsync(meetingId);
            Assert.NotNull(byId);
            Assert.Equal(venueId, byId.Venue.Id);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var context = await new MeetingsRepository(db).GetRegistrationContextAsync(meetingId);
            Assert.NotNull(context);
            Assert.Equal(venueId, context.Venue.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_with_nonexistent_venue_throws_DbUpdateException()
    {
        var (_, meetingId) = await CreateVenueAndMeetingAsync();
        var nonexistentVenueId = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();
        var repository = new MeetingsRepository(db);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.UpdateAsync(
            meetingId,
            "Updated Title",
            "Updated description",
            "Published",
            DateTimeOffset.UtcNow.AddDays(2),
            DateTimeOffset.UtcNow.AddDays(2).AddHours(1),
            nonexistentVenueId));
    }

    [Fact]
    public async Task CreateAsync_registration_links_attendee_to_meeting_and_is_visible()
    {
        var (_, meetingId) = await CreateVenueAndMeetingAsync();
        var attendeeId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            await new RegistrationsRepository(db).CreateAttendeeAsync(new Attendee
            {
                Id = attendeeId,
                FullName = "Test Attendee",
                Email = "attendee@test.example"
            });
        }

        await using (var db = _fixture.CreateDbContext())
        {
            await new RegistrationsRepository(db).CreateAsync(new Registration
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                AttendeeId = attendeeId,
                RegisteredAt = DateTimeOffset.UtcNow,
                TicketType = "General",
                PaymentStatus = "Pending"
            });
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var all = await new RegistrationsRepository(db).GetAllAsync();
            Assert.Contains(all, r => r.AttendeeId == attendeeId && r.MeetingId == meetingId);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var byMeeting = await new RegistrationsRepository(db).GetByMeetingAsync(meetingId);
            Assert.Contains(byMeeting, r => r.AttendeeId == attendeeId);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var attendee = await new RegistrationsRepository(db).GetAttendeeAsync(attendeeId);
            Assert.NotNull(attendee);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var allAttendees = await new RegistrationsRepository(db).GetAllAttendeesAsync();
            Assert.Contains(allAttendees, a => a.Id == attendeeId);
        }
    }

    [Fact]
    public async Task DeleteAttendeeAsync_with_existing_registration_returns_HasDependencies()
    {
        var (_, meetingId) = await CreateVenueAndMeetingAsync();
        var attendeeId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            await new RegistrationsRepository(db).CreateAttendeeAsync(new Attendee
            {
                Id = attendeeId,
                FullName = "Attendee With Registration",
                Email = "with-registration@test.example"
            });
        }

        await using (var db = _fixture.CreateDbContext())
        {
            await new RegistrationsRepository(db).CreateAsync(new Registration
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                AttendeeId = attendeeId,
                RegisteredAt = DateTimeOffset.UtcNow,
                TicketType = "General",
                PaymentStatus = "Pending"
            });
        }

        await using var db2 = _fixture.CreateDbContext();
        var result = await new RegistrationsRepository(db2).DeleteAttendeeAsync(attendeeId);

        Assert.Equal(DeleteResult.HasDependencies, result);
    }

    [Fact]
    public async Task DeleteAttendeeAsync_with_no_dependencies_returns_Deleted()
    {
        var attendeeId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            await new RegistrationsRepository(db).CreateAttendeeAsync(new Attendee
            {
                Id = attendeeId,
                FullName = "Attendee Without Registration",
                Email = "no-registration@test.example"
            });
        }

        await using var db2 = _fixture.CreateDbContext();
        var result = await new RegistrationsRepository(db2).DeleteAttendeeAsync(attendeeId);

        Assert.Equal(DeleteResult.Deleted, result);
    }
}
