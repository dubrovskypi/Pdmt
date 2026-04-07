import { useState, useEffect, useCallback } from "react";
import { EventType } from "@/api/types";
import type { EventResponseDto, TagResponseDto } from "@/api/types";
import { getEvents } from "@/api/events";
import { getTags } from "@/api/tags";
import type { EventFilters } from "@/api/events";

export type TypeFilter = "all" | "pos" | "neg";

function getDateString(daysAgo: number = 0): string {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

interface UseEventListReturn {
  events: EventResponseDto[];
  allTags: TagResponseDto[];
  loading: boolean;
  error: string | null;
  from: string;
  setFrom: (v: string) => void;
  to: string;
  setTo: (v: string) => void;
  typeFilter: TypeFilter;
  setTypeFilter: (v: TypeFilter) => void;
  tagIds: string[];
  toggleTag: (id: string) => void;
  resetFilters: () => void;
  refresh: () => void;
  isFiltersEmpty: boolean;
}

export function useEventList(): UseEventListReturn {
  const [events, setEvents] = useState<EventResponseDto[]>([]);
  const [allTags, setAllTags] = useState<TagResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [from, setFrom] = useState(getDateString(7));
  const [to, setTo] = useState(getDateString());
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("all");
  const [tagIds, setTagIds] = useState<string[]>([]);

  const loadTags = useCallback(async () => {
    try {
      const tags = await getTags();
      setAllTags(tags);
    } catch (err) {
      setError("Не удалось загрузить теги.");
      console.error("Failed to load tags:", err);
    }
  }, []);

  const loadEvents = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const filters: EventFilters = {
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to + "T23:59:59.999Z").toISOString() : undefined,
        type:
          typeFilter === "pos"
            ? EventType.Positive
            : typeFilter === "neg"
              ? EventType.Negative
              : undefined,
        tags: tagIds.length > 0 ? tagIds.join(",") : undefined,
      };
      const evts = await getEvents(filters);
      setEvents(evts);
    } catch (err) {
      setError("Не удалось загрузить события.");
      console.error("Failed to load events:", err);
    } finally {
      setLoading(false);
    }
  }, [from, to, typeFilter, tagIds]);

  // Load tags once on mount
  useEffect(() => {
    void loadTags();
  }, [loadTags]);

  // Load events when filters change
  useEffect(() => {
    void loadEvents();
  }, [loadEvents]);

  const toggleTag = useCallback((tagId: string) => {
    setTagIds((prev) =>
      prev.includes(tagId)
        ? prev.filter((id) => id !== tagId)
        : [...prev, tagId],
    );
  }, []);

  const resetFilters = useCallback(() => {
    setFrom("");
    setTo("");
    setTypeFilter("all");
    setTagIds([]);
  }, []);

  const refresh = useCallback(() => {
    void loadEvents();
  }, [loadEvents]);

  const isFiltersEmpty =
    from === "" && to === "" && typeFilter === "all" && tagIds.length === 0;

  return {
    events,
    allTags,
    loading,
    error,
    from,
    setFrom,
    to,
    setTo,
    typeFilter,
    setTypeFilter,
    tagIds,
    toggleTag,
    resetFilters,
    refresh,
    isFiltersEmpty,
  };
}
