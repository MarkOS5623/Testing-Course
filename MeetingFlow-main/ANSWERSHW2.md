# Homework: Component and Integration Tests — Answers

## Part 1 — Design the test strategy first

> Review the architecture in `MeetingFlow.Microservices/README.md` and inspect the `Gateway`, `Managers`, `Engines`, and `Accessors` folders. Identify the public boundary of the complete backend, the boundary of each individual microservice, synchronous HTTP dependencies, asynchronous messaging dependencies, and infrastructure owned by or required by each service.

### Architecture identification

| Service | Boundary (responsibility) | Synchronous HTTP deps | Asynchronous messaging deps | Infrastructure owned/required |
| --- | --- | --- | --- | --- |
| Gateway | Public HTTP edge — routes to Managers/Engines, maps to public DTOs | → MeetingsManager, RegistrationsManager, AiChatEngine | none | none |
| MeetingsManager | Meeting/session/speaker orchestration | → DataAccessor, SchedulingEngine | none | none |
| RegistrationsManager | Registration + feedback orchestration, pricing | → DataAccessor, SchedulingEngine | publishes `registration.created.v1` | none directly (`RabbitMQ.Client` package, but no DB package) |
| SchedulingEngine | Pure logic — conflict detection, capacity checks | none — zero dependencies of any kind | none | none |
| AiChatEngine | AI chat with action execution | → DataAccessor | none | external AI provider (config-driven endpoint/model/key via `Microsoft.Extensions.AI.OpenAI`) |
| DataAccessor | EF Core CRUD over Postgres (meetings, registrations, feedback schemas) | none — bottom of the call stack | none | Postgres (direct, via `POSTGRES_CONN`) |
| NotificationsAccessor | Notification persistence + fake email sending | none | consumes `registration.created.v1` | Postgres (`notifications` schema) + RabbitMQ (consumer) |

### Test strategy table

> Before writing tests, think about how you would fill out this table.

| Area | Proposed test level | Entry point | What should be real? | What can be replaced? |
| --- | --- | --- | --- | --- |
| Scheduling rules | Component | `POST /scheduling/check-conflict` and `POST /scheduling/check-capacity` | The engine logic | Everything — there is nothing to fake in this test because the engine has no dependencies |
| Data persistence | Component, with a real Postgres instance | Repository classes directly constructed with `MeetingFlowDbContext` | Postgres and DataAccessor's own models | Nothing — DataAccessor has zero dependencies of its own |
| Registration orchestration | Component | `POST /registrations` via `WebApplicationFactory<RegistrationsManager Program>` | RegistrationsManager's own endpoint code — validation, duplicate checks, capacity decision, pricing calls, persist-then-publish ordering | `DataAccessorClient` and `SchedulingEngineClient`, faked to return whatever seeded scenario I want to test. `IEventPublisher` faked only to check whether it was called, not to test RabbitMQ itself |
| Notification delivery | Component | Extracted `RegistrationNotificationHandler.HandleAsync` | Postgres, `FakeSmtpGateway` | RabbitMQ |
| Complete registration flow | System | Gateway `POST /registrations` | Everything | Nothing |

---

## Part 2 — Component test plan: Data persistence (DataAccessor)

> First identify the selected service's responsibility and choose behaviors that give useful confidence in that responsibility. Decide which dependencies should remain real, which may be controlled or replaced, and what observable result will prove the behavior.

**Service responsibility:** DataAccessor is a pure persistence layer — EF Core CRUD over Postgres for the `meetings`, `registrations`, and `feedback` schemas. It has no business logic of its own beyond a small number of guards written directly into the repositories: a venue-existence check on meeting creation, and dependency checks before deleting an attendee. There's nothing to fake — it's the bottom of the call stack (see the Part 1 table).

**Why these behaviors:** the seven scenarios below were chosen because they're the only things in this service actually worth confidence in — a create genuinely landing in the database, a read correctly eager-loading its relationships, the one place a real constraint (not application code) is the only thing enforcing correctness, and both branches of the one real conditional in the repository layer. Generic "does CRUD work" coverage was deliberately rejected in favor of these specific, meaningful behaviors.

**What should be real / what can be replaced:** real Postgres for all seven (via Testcontainers) — nothing is faked, since DataAccessor has zero dependencies of its own and the whole point of this row is proving real SQL/schema/constraint behavior that an in-memory provider would hide.

Detailed scenario plan, each with the observable result that proves it:

1. `CreateVenueAsync` — seed a venue. **Proves:** the venue row is actually persisted and readable back from a fresh `DbContext`, not just held in the change tracker.
2. `CreateMeetingAsync` — create a meeting referencing that venue. **Proves:** the returned meeting is non-null with its `Venue` navigation populated, confirming the venue-existence guard passes on a valid reference.
3. `GetAllAsync` / `GetByIdAsync` / `GetRegistrationContextAsync` — confirm visibility and correct `Venue` eager-loading. **Proves:** all three read paths correctly `.Include()` the venue relationship, not just one of them.
4. `UpdateAsync` with a nonexistent `venueId`. **Proves:** whether Postgres's FK constraint catches what `UpdateAsync`'s own C# code doesn't check — the one scenario in this plan that can only be answered by a real database engine.
5. `CreateAttendeeAsync` (attendee A) → `CreateAsync(Registration)` linking attendee A to the meeting → `GetAllAsync` / `GetByMeetingAsync` confirm the registration is visible → `GetAttendeeAsync` / `GetAllAttendeesAsync` confirm attendee A is visible. **Proves:** the registration join actually persists and is visible from every read path that's supposed to surface it.
6. `DeleteAttendeeAsync(attendee A)`. **Proves:** the `HasDependencies` guard rejects deletion while a registration exists — the rejection branch of the one real conditional in this repository.
7. `CreateAttendeeAsync` (attendee B, no registration) → `DeleteAttendeeAsync(attendee B)`. **Proves:** the success branch — `Deleted` is returned when there are no dependencies, so the guard isn't just always rejecting.

Tests are implemented and passing — see `MeetingFlow.Microservices/tests/MeetingFlow.ComponentTests/`.

---

## Part 3 — Targeted integration test: RabbitMQ producer/consumer

> Choose an integration between two real application components and prove that they can communicate using their production contract. The test should answer one focused question: are these two components really compatible? Do not start the complete MeetingFlow system for this test.

**Boundary chosen:** RegistrationsManager's `EventPublisher` → real RabbitMQ → NotificationsAccessor's `RegistrationEventConsumer`.

**Why this boundary:** `EventPublisher.cs` and `RegistrationEventConsumer.cs` each independently hardcode their own copy of the exchange name and routing key as string literals.The payload `RegistrationCreatedV1` is already compile-time guaranteed by the shared `MeetingFlow.IntegrationEvents`. But nothing guarantees the strings stay in sync and a mismatch there would compile fine and pass both service's tests in isolation while silently dropping every message without raising an error. This is exactly a "are these two components really compatible" question a unit or component test cannot answer.

**What's real:** the production `EventPublisher` and `RegistrationEventConsumer` classes, an unmodified real RabbitMQ broker and a real Postgres(both using testcontainers), since the saved `Notification` row is the only observable proof we have that the consumer actually received and processed the message.

**What's out of scope:** everything else: None of the other services are started. The event is published directly via `EventPublisher.PublishAsync(...)`, not through `POST /registrations`.

**The one test:** publish a `RegistrationCreatedV1` with a known `AttendeeId`, poll for a matching `Notification` row, and assert its `Subject`/`Body` reflect the event's data.

**Proves:** the exchange/queue/routing-key wiring between the two independently-hardcoded copies actually agrees, and the shared contract serializes and deserializes correctly across a real broker.

Implemented and passing — see `MeetingFlow.Microservices/tests/MeetingFlow.IntegrationTests/`.

---

## Part 4 — Backend system test: complete registration flow

> Cover the complete registration flow through the public backend boundary. Use real services and real infrastructure for this flow. Decide how to run the environment, prepare the scenario, observe the result, and handle the data.

**Environment:** the test's own fixture drives `docker compose up -d --build --wait` on setup and `docker compose down` on teardown, so `dotnet test` is self-contained the same way Parts 2 and 3 are — no manual precondition step. Testcontainers has no mature "run an existing docker-compose.yml" module for .NET, so this is a direct `docker compose` CLI invocation via `System.Diagnostics.Process`, not a Testcontainers abstraction. After Docker reports the containers healthy/running (`--wait`), the fixture additionally polls `GET /meetings` through Gateway until it succeeds, since `--wait` only confirms the containers are up, not that each ASP.NET app has finished EF `EnsureCreated` + seeding — the same distinction Part 0 implicitly relies on when it asks you to verify `/meetings` returns data, not just that the container is running.

**Scenario preparation:** rather than depend on DataAccessor's seeded IDs, the test creates its own data through the public API — `POST /venues` → `POST /meetings` (status `Published`) → `POST /attendees` — so the test is self-documenting and doesn't silently depend on seed data shape staying the same.

**Observing the result**, matching the three things Part 4 asks the test to prove:

1. **A registration can be created through Gateway** — `POST /registrations` returns success.

2. **The saved registration can be read again** — `GET /registrations/by-meeting/{meetingId}` is called afterward and must contain it.

3. **The registration notification is eventually created** — polled (not asserted immediately, since delivery through RabbitMQ is asynchronous) via `GET /notifications/by-attendee/{attendeeId}` on **NotificationsAccessor directly** (`localhost:5011`), not through Gateway — Gateway has no route for notifications at all, so this is the only way to observe requirement 3.

**Data handling:** no manual cleanup between runs — Postgres in `docker-compose.yml` has no persistent volume mount for its data directory, so `docker compose down` discards all data and the next `up` starts from a clean, freshly-seeded database.

**What's real / what's replaced:** everything is real — all seven services, Postgres, RabbitMQ, built and run exactly as `docker-compose.yml` defines them. Nothing is faked. This intentionally does not repeat the rejection-branch scenarios already covered by Registration orchestration (Part 2) or the RabbitMQ wire-compatibility check (Part 3) — one happy path, proving the deployed pieces work together.

Implemented — see `MeetingFlow.Microservices/tests/MeetingFlow.SystemTests/`.
