import { apiGet, apiPost } from "./client";
import type { TagResponseDto } from "./types";

// Tags change rarely — cache for 60 seconds and coalesce parallel requests.
const TAGS_TTL = 60_000;
let tagsCache: { data: TagResponseDto[]; ts: number } | null = null;
let tagsInflight: Promise<TagResponseDto[]> | null = null;

export function getTags(): Promise<TagResponseDto[]> {
  if (tagsCache && Date.now() - tagsCache.ts < TAGS_TTL) {
    return Promise.resolve(tagsCache.data);
  }
  if (tagsInflight) return tagsInflight;

  tagsInflight = apiGet<TagResponseDto[]>("/api/tags")
    .then((data) => {
      tagsCache = { data, ts: Date.now() };
      return data;
    })
    .finally(() => {
      tagsInflight = null;
    });

  return tagsInflight;
}

export async function createTag(name: string, signal?: AbortSignal): Promise<TagResponseDto> {
  const tag = await apiPost<TagResponseDto>("/api/tags", { name }, signal);
  invalidateTagsCache();
  return tag;
}

export function invalidateTagsCache(): void {
  tagsCache = null;
  tagsInflight = null;
}
