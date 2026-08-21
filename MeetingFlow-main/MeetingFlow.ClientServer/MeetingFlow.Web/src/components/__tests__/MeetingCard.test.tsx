import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import MeetingCard from "../MeetingCard";
import type { Meeting } from "../../types/models";

function createMeeting(overrides: Partial<Meeting> = {}): Meeting {
  return {
    id: "meeting-1",
    title: "Test Meeting",
    description: "A test meeting description",
    status: "Published",
    startsAt: "2026-01-01T00:00:00Z",
    endsAt: "2026-01-01T01:00:00Z",
    createdAt: "2025-01-01T00:00:00Z",
    venueId: "venue-1",
    venue: { id: "venue-1", name: "Test Venue", address: "123 Test St", city: "Testville", capacity: 100, meetings: [] },
    sessions: [],
    registrations: [],
    feedback: [],
    ...overrides,
  };
}

function renderMeetingCard(meeting: Meeting) { render(<MemoryRouter> <MeetingCard meeting={meeting}/></MemoryRouter>);}

describe("MeetingCard badge", () => {
  it("renders badge-published for a Published meeting", () => {
    renderMeetingCard(createMeeting({ status: "Published" }));
    const badge = screen.getByText("Published");
    expect(badge).toHaveClass("badge-published");
  });

  it("renders badge-draft for a Draft meeting", () => {
    renderMeetingCard(createMeeting({ status: "Draft" }));
    const badge = screen.getByText("Draft");
    expect(badge).toHaveClass("badge-draft");
  });

  it("renders badge-cancelled for a Cancelled meeting", () => {
    renderMeetingCard(createMeeting({ status: "Cancelled" }));
    const badge = screen.getByText("Cancelled");
    expect(badge).toHaveClass("badge-cancelled");
  });
});
