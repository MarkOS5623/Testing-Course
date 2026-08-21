using System.Net.Http.Json;

namespace MeetingFlow.SystemTests;

// The one critical happy path through the deployed system:
// Gateway -> RegistrationsManager -> DataAccessor -> Postgres
//                              |-> SchedulingEngine
//                              '-> RabbitMQ -> NotificationsAccessor -> Postgres
//
// Deliberately does not repeat the rejection-branch scenarios already covered by the
// Registration orchestration component tests (Part 2) or the RabbitMQ wire-compatibility
// integration test (Part 3) - this proves the real, deployed pieces work together, once.
public class RegistrationFlowSystemTests : IClassFixture<DockerComposeFixture>
{
    static readonly HttpClient Gateway = new() { BaseAddress = new Uri("http://localhost:8080") };

    // NotificationsAccessor is not proxied by Gateway at all - it has no route for it -
    // so observing "was a notification created" necessarily means reaching this service
    // directly on its own published port, the only way this requirement can be checked.
    static readonly HttpClient NotificationsAccessor = new() { BaseAddress = new Uri("http://localhost:5011") };

    [Fact]
    public async Task Registration_created_through_Gateway_is_persisted_and_triggers_a_notification()
    {
        var venue = await PostAsync<VenueDto>("/venues", new
        {
            name = "System Test Venue",
            address = "1 System Test Way",
            city = "Testville",
            capacity = 100
        });

        var meeting = await PostAsync<MeetingDetailsDto>("/meetings", new
        {
            title = "System Test Conference",
            description = "Created by the Part 4 system test.",
            status = "Published",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            endsAt = DateTimeOffset.UtcNow.AddDays(10).AddHours(3),
            venueId = venue.Id
        });

        var attendee = await PostAsync<AttendeeDto>("/attendees", new
        {
            fullName = "System Test Attendee",
            email = "system-test@test.example",
            phone = (string?)null,
            company = (string?)null
        });

        // Requirement 1: a registration can be created through Gateway.
        var registrationResponse = await Gateway.PostAsJsonAsync("/registrations", new
        {
            meetingId = meeting.Id,
            attendeeId = attendee.Id,
            ticketType = "General"
        });
        registrationResponse.EnsureSuccessStatusCode();
        var created = await registrationResponse.Content.ReadFromJsonAsync<CreateRegistrationResult>();
        Assert.NotNull(created);

        // Requirement 2: the saved registration can be read again.
        var byMeeting = await Gateway.GetFromJsonAsync<List<RegistrationDto>>(
            $"/registrations/by-meeting/{meeting.Id}");
        Assert.NotNull(byMeeting);
        Assert.Contains(byMeeting, r => r.AttendeeId == attendee.Id);

        // Requirement 3: the registration notification is eventually created.
        // Delivery is asynchronous (Gateway -> RegistrationsManager -> RabbitMQ ->
        // NotificationsAccessor), so this has to poll rather than assert immediately.
        var notification = await PollForNotificationAsync(attendee.Id, TimeSpan.FromSeconds(30));
        Assert.NotNull(notification);
    }

    static async Task<T> PostAsync<T>(string path, object body)
    {
        var response = await Gateway.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result!;
    }

    static async Task<NotificationDto?> PollForNotificationAsync(Guid attendeeId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var notifications = await NotificationsAccessor.GetFromJsonAsync<List<NotificationDto>>(
                $"/notifications/by-attendee/{attendeeId}");
            if (notifications is { Count: > 0 }) return notifications[0];

            await Task.Delay(500);
        }

        return null;
    }

    record VenueDto(Guid Id, string Name, string Address, string City, int Capacity);
    record MeetingDetailsDto(Guid Id, string Title);
    record AttendeeDto(Guid Id, string FullName, string Email);
    record RegistrationDto(Guid Id, Guid MeetingId, Guid AttendeeId, DateTimeOffset RegisteredAt, string TicketType, string PaymentStatus);
    record CreateRegistrationResult(RegistrationDto Registration, decimal CalculatedPrice);
    record NotificationDto(Guid Id, Guid AttendeeId, string Type, string Subject, string Body, DateTimeOffset? SentAt);
}
