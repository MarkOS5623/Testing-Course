# MeetingFlow.Web

React + TypeScript + Vite single-page application that consumes the MeetingFlow.Api backend.

## Architecture

```
MeetingFlow.Web/
├── index.html                      # Vite entry point
├── package.json                    # Dependencies (React, React Router, Vite)
├── vite.config.ts                  # Dev server config — proxies /api to the backend
├── tsconfig.json                   # TypeScript configuration
└── src/
    ├── main.tsx                    # React DOM root
    ├── App.tsx                     # Router setup + navigation layout
    ├── App.css                     # Global styles
    ├── types/
    │   └── models.ts               # TypeScript types mirroring backend EF Core entities
    ├── api/
    │   ├── http.ts                 # Base HTTP client (get/post helpers against /api)
    │   ├── meetingsApi.ts          # Meeting CRUD calls
    │   ├── registrationsApi.ts     # Registration creation
    │   ├── speakersApi.ts          # Speaker profile fetch
    │   ├── dashboardApi.ts         # Dashboard analytics fetch
    │   └── auditLogApi.ts          # Audit log fetch
    ├── pages/
    │   ├── MeetingsPage.tsx        # Public meeting list
    │   ├── MeetingDetailsPage.tsx  # Single meeting with sessions, feedback, registrations
    │   ├── CreateRegistrationPage.tsx # Registration form
    │   ├── DashboardPage.tsx       # Aggregate stats + upcoming meetings
    │   ├── SpeakerProfilePage.tsx  # Speaker bio + sessions
    │   ├── AdminMeetingsPage.tsx   # Admin meeting list (shows internal fields)
    │   └── AuditLogPage.tsx        # Audit log viewer
    └── components/
        ├── MeetingCard.tsx         # Meeting summary card
        ├── MeetingTable.tsx        # Tabular meeting display
        ├── SessionList.tsx         # Session listing within a meeting
        ├── SpeakerCard.tsx         # Speaker summary display
        └── FeedbackList.tsx        # Feedback listing
```

This project intentionally mirrors backend EF Core entity types 1:1 as TypeScript types. The same large model is used across all pages, even when a page only needs a few fields.

### Tech Stack

- **React 18** with functional components and hooks
- **TypeScript** — strict mode
- **Vite** — dev server with proxy to backend at `localhost:5173`
- **React Router v6** — client-side routing

## Pages and Routes

| Route                | Page Component           | API Call             | Description                              |
| -------------------- | ------------------------ | -------------------- | ---------------------------------------- |
| `/`                  | `MeetingsPage`           | `GET /api/meetings`  | Public meeting list with venue info      |
| `/meetings/:id`      | `MeetingDetailsPage`     | `GET /api/meetings/{id}` | Full meeting details                 |
| `/register`          | `CreateRegistrationPage` | `POST /api/registrations` | Registration form                   |
| `/dashboard`         | `DashboardPage`          | `GET /api/dashboard` | Aggregate stats + upcoming meetings      |
| `/speakers/:id`      | `SpeakerProfilePage`     | `GET /api/speakers/{id}` | Speaker profile                     |
| `/admin/meetings`    | `AdminMeetingsPage`      | `GET /api/admin/meetings` | Admin meeting list with internal fields |
| `/admin/audit-log`   | `AuditLogPage`           | `GET /api/audit-log` | Audit log viewer                         |

## Main Flows

### 1. Browse Meetings

`MeetingsPage` → calls `GET /api/meetings` → receives full `Meeting[]` with Venue and Sessions → renders `MeetingCard` components with title, date, venue.

### 2. View Meeting Details

`MeetingDetailsPage` → calls `GET /api/meetings/{id}` → receives the complete Meeting entity graph → renders `SessionList` (with speaker links), `FeedbackList`, and registration info.

### 3. Register for a Meeting

`CreateRegistrationPage` → loads available meetings for a dropdown → user fills in name, email, ticket type → `POST /api/registrations` with `{ meetingId, attendeeName, attendeeEmail, ticketType }`.

### 4. Dashboard

`DashboardPage` → calls `GET /api/dashboard` → displays totalMeetings, totalRegistrations, totalSpeakers, averageFeedbackRating, and a list of upcoming meetings.

### 5. Speaker Profile

`SpeakerProfilePage` → calls `GET /api/speakers/{id}` → renders speaker bio, contact info, and their sessions.

### 6. Admin Views

`AdminMeetingsPage` and `AuditLogPage` call their respective admin endpoints with no authentication. Internal fields are rendered directly.

## API Layer

All API calls go through `src/api/http.ts`, which provides typed `get<T>` and `post<T>` helpers against the `/api` base path. The Vite dev server proxies `/api` requests to the backend.

## Running

```bash
npm install
npm run dev
```

The app runs at `http://localhost:5173` and expects the API backend to be running (proxied via Vite config).

## What's Intentionally Wrong

- TypeScript types in `models.ts` mirror backend EF Core entities 1:1
- All pages use the same large `Meeting` type even when they need different subsets
- Internal fields (`internalNotes`, `adminOnlyCode`) are present in the shared type
- No page-specific or component-specific types
- Admin pages have no authentication checks
