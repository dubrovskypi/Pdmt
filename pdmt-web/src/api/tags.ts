import { apiGet, apiPost } from "./client";
import type { TagResponseDto } from "./types";

export const getTags = () => apiGet<TagResponseDto[]>("/api/tags");

export const createTag = (name: string) =>
  apiPost<TagResponseDto>("/api/tags", { name });
