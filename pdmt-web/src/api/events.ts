import { apiGet, apiPost, apiPut, apiDelete } from "./client";
import type { EventResponseDto, CreateEventDto, UpdateEventDto } from "./types";
import { EventResponseSchema } from "./schemas";

export interface EventFilters {
  from?: string;
  to?: string;
  type?: number;
  tags?: string; // comma-separated Guid IDs
  minIntensity?: number;
  maxIntensity?: number;
}

function buildQuery(filters: EventFilters): string {
  const q = new URLSearchParams();
  if (filters.from) q.set("from", filters.from);
  if (filters.to) q.set("to", filters.to);
  if (filters.type !== undefined) q.set("type", String(filters.type));
  if (filters.tags) q.set("tags", filters.tags);
  if (filters.minIntensity !== undefined) q.set("minIntensity", String(filters.minIntensity));
  if (filters.maxIntensity !== undefined) q.set("maxIntensity", String(filters.maxIntensity));
  const s = q.toString();
  return s ? `/api/events?${s}` : "/api/events";
}

export const getEvents = (filters: EventFilters = {}, signal?: AbortSignal) =>
  apiGet<EventResponseDto[]>(buildQuery(filters), signal);

export const getEventById = async (id: string, signal?: AbortSignal) =>
  EventResponseSchema.parse(
    await apiGet<EventResponseDto>(`/api/events/${id}`, signal),
  ) as EventResponseDto;

export const createEvent = async (dto: CreateEventDto, signal?: AbortSignal) =>
  EventResponseSchema.parse(
    await apiPost<EventResponseDto>("/api/events", dto, signal),
  ) as EventResponseDto;

export const updateEvent = (id: string, dto: UpdateEventDto, signal?: AbortSignal) =>
  apiPut(`/api/events/${id}`, dto, signal);

export const deleteEvent = (id: string, signal?: AbortSignal) =>
  apiDelete(`/api/events/${id}`, signal);
