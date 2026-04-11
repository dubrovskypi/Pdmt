import { apiGet, apiPost } from "./client";
import type { TagResponseDto } from "./types";

export const getTags = (signal?: AbortSignal) =>
  apiGet<TagResponseDto[]>("/api/tags", signal);

export const createTag = (name: string, signal?: AbortSignal) =>
  apiPost<TagResponseDto>("/api/tags", { name }, signal);
