import { getTags, createTag, invalidateTagsCache } from "./tags";
import * as client from "./client";

vi.mock("./client");

const mockApiGet = vi.mocked(client.apiGet);
const mockApiPost = vi.mocked(client.apiPost);

const SAMPLE_TAGS = [
  { id: "tag-1", name: "work" },
  { id: "tag-2", name: "health" },
];

beforeEach(() => {
  invalidateTagsCache();
  vi.clearAllMocks();
});

// ─── getTags caching ──────────────────────────────────────────────────────────

describe("getTags", () => {
  it("fetches from API on first call", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    const result = await getTags();
    expect(mockApiGet).toHaveBeenCalledOnce();
    expect(result).toEqual(SAMPLE_TAGS);
  });

  it("returns cached data on second call without hitting the API", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    await getTags();
    const result = await getTags();
    expect(mockApiGet).toHaveBeenCalledOnce();
    expect(result).toEqual(SAMPLE_TAGS);
  });

  it("coalesces concurrent calls into a single request", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    const [r1, r2] = await Promise.all([getTags(), getTags()]);
    expect(mockApiGet).toHaveBeenCalledOnce();
    expect(r1).toEqual(SAMPLE_TAGS);
    expect(r2).toEqual(SAMPLE_TAGS);
  });

  it("fetches again after invalidateTagsCache", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    await getTags();
    invalidateTagsCache();
    await getTags();
    expect(mockApiGet).toHaveBeenCalledTimes(2);
  });

  it("passes signal to apiGet", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    await getTags();
    expect(mockApiGet).toHaveBeenCalledWith("/api/tags");
  });
});

// ─── createTag ────────────────────────────────────────────────────────────────

describe("createTag", () => {
  it("posts to /api/tags and returns the new tag", async () => {
    const newTag = { id: "tag-3", name: "sport" };
    mockApiPost.mockResolvedValue(newTag);
    const result = await createTag("sport");
    expect(mockApiPost).toHaveBeenCalledWith("/api/tags", { name: "sport" }, undefined);
    expect(result).toEqual(newTag);
  });

  it("invalidates the tags cache after creating a tag", async () => {
    mockApiGet.mockResolvedValue(SAMPLE_TAGS);
    mockApiPost.mockResolvedValue({ id: "tag-3", name: "sport" });

    await getTags(); // populates cache
    await createTag("sport"); // should invalidate cache
    await getTags(); // should fetch again

    expect(mockApiGet).toHaveBeenCalledTimes(2);
  });

  it("passes signal to apiPost", async () => {
    mockApiPost.mockResolvedValue({ id: "tag-3", name: "sport" });
    const signal = new AbortController().signal;
    await createTag("sport", signal);
    expect(mockApiPost).toHaveBeenCalledWith("/api/tags", { name: "sport" }, signal);
  });
});
