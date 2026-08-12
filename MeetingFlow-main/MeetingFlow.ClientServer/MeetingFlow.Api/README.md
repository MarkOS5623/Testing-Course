# MeetingFlow.Api

ASP.NET Core Web API using Minimal APIs, EF Core (SQLite), and Hot Chocolate (GraphQL).

## Architecture

```
MeetingFlow.Api/
├── Program.cs                          # App startup: DI, CORS, EF Core, GraphQL, endpoint mapping
├── Models/                             # EF Core entity classes (returned directly from endpoints)
│   ├── Meeting.cs                      # Core aggregate
│   ├── Session.cs                      # Talk/slot within a meeting
│   ├── Speaker.cs                      # Speaker profile with contact info
│   ├── Venue.cs                        # Physical location
│   ├── Registration.cs                 # Attendee ↔ Meeting join
│   ├── Attendee.cs                     # Registered person
│   ├── Feedback.cs                     # Post-meeting rating + comments
│   ├── Notification.cs                 # Email/SMS log
│   └── AuditLogEntry.cs               # System audit trail
├── Data/
│   ├── MeetingFlowDbContext.cs         # EF Core context with all DbSets and relationships
│   └── SeedData.cs                     # Demo data on first run
└── Endpoints/
    ├── MeetingsEndpoints.cs            # CRUD for meetings + admin list
    ├── RegistrationsEndpoints.cs       # Create registration
    ├── SpeakersEndpoints.cs            # Speaker profile lookup
    ├── DashboardEndpoints.cs           # Aggregate analytics
    ├── AuditLogEndpoints.cs            # Audit log listing
    └── GraphQlSetup.cs                 # Hot Chocolate query root (exposes entities via GraphQL)
```

This project intentionally returns EF Core entity models directly from API endpoints. There are no DTOs, response models, or mapping layers.

### Tech Stack

- **ASP.NET Core 9** — Minimal APIs
- **EF Core** — SQLite provider
- **Hot Chocolate** — GraphQL with projections, filtering, sorting
- **CORS** enabled for React dev server (`localhost:5173`)
- **System.Text.Json** with `ReferenceHandler.IgnoreCycles`

## REST Endpoints

| Method | Path                  | Description                                                    |
| ------ | --------------------- | -------------------------------------------------------------- |
| GET    | `/api/meetings`       | List all meetings with Venue and Sessions                      |
| GET    | `/api/meetings/{id}`  | Meeting details with full entity graph                         |
| GET    | `/api/admin/meetings` | Admin view — includes InternalNotes, AdminOnlyCode             |
| PUT    | `/api/meetings/{id}`  | Update meeting (accepts full entity body)                      |
| GET    | `/api/speakers/{id}`  | Speaker profile with sessions and meeting info                 |
| POST   | `/api/registrations`  | Create registration (accepts CreateRegistrationRequest record) |
| GET    | `/api/dashboard`      | Dashboard analytics (returns anonymous object)                 |
| GET    | `/api/audit-log`      | Audit log entries                                              |

## GraphQL

Available at `/graphql` with the Banana Cake Pop IDE.

**Query root** (`GraphQlSetup.Query`):

- `meetings` — `IQueryable<Meeting>` with `[UseProjection]`, `[UseFiltering]`, `[UseSorting]`
- `meetingById(id)` — Single meeting with eager-loaded Venue, Sessions, Speakers, Registrations, Feedback
- `speakers` — `IQueryable<Speaker>` with projection/filtering/sorting
- `speakerById(id)` — Single speaker with sessions

The GraphQL schema is generated directly from EF Core entity types.

## Main Flows

### 1. List Meetings

`GET /api/meetings` → queries `Meetings` with `Include(Venue)` and `Include(Sessions)` → returns the full list sorted by `StartsAt`.

### 2. Meeting Details

`GET /api/meetings/{id}` → eager-loads Venue, Sessions → Speaker, Registrations → Attendee, Feedback → Attendee → returns the complete entity graph.

### 3. Create Registration

`POST /api/registrations` → accepts a `CreateRegistrationRequest(MeetingId, AttendeeName, AttendeeEmail, TicketType)` → finds or creates an `Attendee` by email → creates a `Registration` with `PaymentStatus = "Pending"` → returns the entity.

### 4. Update Meeting

`PUT /api/meetings/{id}` → accepts full `Meeting` entity body → updates Title, Description, Status, dates, InternalNotes, AdminOnlyCode, VenueId → saves.

### 5. Dashboard Analytics

`GET /api/dashboard` → runs count/average queries → loads upcoming published meetings with Venue and Registrations → returns an anonymous object with `totalMeetings`, `totalRegistrations`, `totalSpeakers`, `averageFeedbackRating`, `upcomingMeetings`.

### 6. Speaker Profile

`GET /api/speakers/{id}` → loads Speaker with Sessions and their Meetings → returns the full entity including Email, Phone, InternalNotes.

### 7. Admin Meeting List

`GET /api/admin/meetings` → loads all meetings with Venue and Registrations → returns sorted by creation date. Includes internal fields.

### 8. Audit Log

`GET /api/audit-log` → returns all `AuditLogEntry` records ordered by date descending, including `TechnicalDetails`.

## Running

```bash
dotnet restore
dotnet run
```

The SQLite database (`meetingflow_api.db`) is created and seeded automatically on startup.

## What's Intentionally Wrong

- All endpoints return full EF Core entities including internal/sensitive fields
- PUT endpoint accepts the full entity directly
- GraphQL exposes the persistence model directly — clients can query InternalNotes, AdminOnlyCode
- Dashboard returns an anonymous object instead of a named type
- No authentication or authorization
- No response shaping or field filtering
