import { getEvents, getEventById, createEvent, updateEvent, deleteEvent } from "./events";
import * as client from "./client";
import { EventType } from "./types";
import type { CreateEventDto, UpdateEventDto } from "./types";

vi.mock("./client");

const mockApiGet = vi.mocked(client.apiGet);
const mockApiPost = vi.mocked(client.apiPost);
const mockApiPut = vi.mocked(client.apiPut);
const mockApiDelete = vi.mocked(client.apiDelete);

afterEach(() => vi.clearAllMocks());

// ─── getEvents URL building ───────────────────────────────────────────────────

describe("getEvents", () => {
  it("uses /api/events with no query string when called with no filters", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents();
    expect(mockApiGet).toHaveBeenCalledWith("/api/events", undefined);
  });

  it("adds from and to filters", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents({ from: "2026-04-01", to: "2026-04-07" });
    expect(mockApiGet).toHaveBeenCalledWith("/api/events?from=2026-04-01&to=2026-04-07", undefined);
  });

  it("adds type filter as numeric value", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents({ type: EventType.Positive });
    expect(mockApiGet).toHaveBeenCalledWith("/api/events?type=1", undefined);
  });

  it("adds intensity range filters", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents({ minIntensity: 3, maxIntensity: 8 });
    expect(mockApiGet).toHaveBeenCalledWith("/api/events?minIntensity=3&maxIntensity=8", undefined);
  });

  it("adds tags as a URL-encoded comma-separated string", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents({ tags: "id-1,id-2" });
    // URLSearchParams encodes commas as %2C; the server decodes them back
    expect(mockApiGet).toHaveBeenCalledWith("/api/events?tags=id-1%2Cid-2", undefined);
  });

  it("omits undefined filter values", async () => {
    mockApiGet.mockResolvedValue([]);
    await getEvents({ from: undefined, type: undefined, tags: undefined });
    expect(mockApiGet).toHaveBeenCalledWith("/api/events", undefined);
  });
});

// ─── other CRUD operations ────────────────────────────────────────────────────

describe("getEventById", () => {
  it("calls the correct endpoint", async () => {
    mockApiGet.mockResolvedValue({ id: "abc" } as never);
    await getEventById("abc");
    expect(mockApiGet).toHaveBeenCalledWith("/api/events/abc", undefined);
  });
});

describe("createEvent", () => {
  it("posts to /api/events with the DTO", async () => {
    mockApiPost.mockResolvedValue({ id: "new" } as never);
    const dto: CreateEventDto = {
      timestamp: "2026-04-07T10:00:00Z",
      type: EventType.Positive,
      intensity: 7,
      title: "Test event",
      canInfluence: false,
      tagNames: [],
    };
    await createEvent(dto);
    expect(mockApiPost).toHaveBeenCalledWith("/api/events", dto, undefined);
  });
});

describe("updateEvent", () => {
  it("puts to /api/events/:id with the DTO", async () => {
    mockApiPut.mockResolvedValue(undefined);
    const dto: UpdateEventDto = {
      timestamp: "2026-04-07T10:00:00Z",
      type: EventType.Negative,
      intensity: 5,
      title: "Updated",
      canInfluence: true,
      tagNames: ["work"],
    };
    await updateEvent("event-1", dto);
    expect(mockApiPut).toHaveBeenCalledWith("/api/events/event-1", dto, undefined);
  });
});

describe("deleteEvent", () => {
  it("calls delete with the correct event ID", async () => {
    mockApiDelete.mockResolvedValue(undefined);
    await deleteEvent("event-1");
    expect(mockApiDelete).toHaveBeenCalledWith("/api/events/event-1", undefined);
  });
});
