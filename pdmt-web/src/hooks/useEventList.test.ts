import { renderHook, waitFor, act } from "@testing-library/react";
import { useEventList } from "./useEventList";
import { getEvents } from "@/api/events";
import { getTags } from "@/api/tags";
import { EventType } from "@/api/types";
import type { EventResponseDto, TagResponseDto } from "@/api/types";

vi.mock("@/api/events");
vi.mock("@/api/tags");

const mockGetEvents = vi.mocked(getEvents);
const mockGetTags = vi.mocked(getTags);

const mockEvents: EventResponseDto[] = [
  {
    id: "1",
    type: EventType.Positive,
    title: "Good thing",
    intensity: 8,
    timestamp: "2026-04-07T10:00:00Z",
    tags: [],
    description: null,
    context: null,
    canInfluence: false,
  },
  {
    id: "2",
    type: EventType.Negative,
    title: "Bad thing",
    intensity: 6,
    timestamp: "2026-04-07T11:00:00Z",
    tags: [],
    description: null,
    context: null,
    canInfluence: false,
  },
];

const mockTags: TagResponseDto[] = [
  { id: "tag-1", name: "work", createdAt: "2026-01-01T00:00:00Z", eventCount: 5 },
  { id: "tag-2", name: "health", createdAt: "2026-01-01T00:00:00Z", eventCount: 3 },
];

describe("useEventList", () => {
  beforeEach(() => {
    mockGetEvents.mockResolvedValue(mockEvents);
    mockGetTags.mockResolvedValue(mockTags);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("loads events and tags on mount", async () => {
    const { result } = renderHook(() => useEventList());

    expect(result.current.loading).toBe(true);

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.events).toEqual(mockEvents);
    expect(result.current.allTags).toEqual(mockTags);
    expect(result.current.error).toBeNull();
  });

  it("sets error when events fail to load", async () => {
    mockGetEvents.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useEventList());

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.error).toBe("Не удалось загрузить данные.");
    expect(result.current.events).toEqual([]);
  });

  it("toggles tag id in active filter", async () => {
    const { result } = renderHook(() => useEventList());
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => result.current.toggleTag("tag-1"));
    expect(result.current.tagIds).toContain("tag-1");

    act(() => result.current.toggleTag("tag-1"));
    expect(result.current.tagIds).not.toContain("tag-1");
  });

  it("resets all filters to empty state", async () => {
    const { result } = renderHook(() => useEventList());
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => {
      result.current.setFrom("2026-01-01");
      result.current.setTypeFilter("pos");
      result.current.toggleTag("tag-1");
    });

    act(() => result.current.resetFilters());

    expect(result.current.from).toBe("");
    expect(result.current.to).toBe("");
    expect(result.current.typeFilter).toBe("all");
    expect(result.current.tagIds).toEqual([]);
    expect(result.current.isFiltersEmpty).toBe(true);
  });

  it("isFiltersEmpty is false when any filter is active", async () => {
    const { result } = renderHook(() => useEventList());
    await waitFor(() => expect(result.current.loading).toBe(false));

    act(() => result.current.setTypeFilter("neg"));

    expect(result.current.isFiltersEmpty).toBe(false);
  });
});
