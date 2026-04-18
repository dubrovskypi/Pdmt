import { useState, useEffect, useCallback } from "react";
import { isAbortError, getErrorMessage } from "@/lib/utils";
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
    } catch (err: unknown) {
      setError(getErrorMessage(err));
      console.error(err);
    }
  }, []);

  const loadEvents = useCallback(
    async (signal?: AbortSignal) => {
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
        const evts = await getEvents(filters, signal);
        setEvents(evts);
      } catch (err: unknown) {
        if (isAbortError(err)) return;
        setError(getErrorMessage(err));
        console.error(err);
      } finally {
        setLoading(false);
      }
    },
    [from, to, typeFilter, tagIds],
  );

  // Load tags once on mount
  useEffect(() => {
    void loadTags();
  }, [loadTags]);

  // Load events when filters change
  useEffect(() => {
    const controller = new AbortController();
    void loadEvents(controller.signal);
    return () => controller.abort();
  }, [loadEvents]);

  const toggleTag = useCallback((tagId: string) => {
    setTagIds((prev) =>
      prev.includes(tagId) ? prev.filter((id) => id !== tagId) : [...prev, tagId],
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

  const isFiltersEmpty = from === "" && to === "" && typeFilter === "all" && tagIds.length === 0;

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
