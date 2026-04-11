import { apiFetch, apiGet, apiPost, apiPut, apiDelete, initApiClient } from "./client";

vi.mock("@/config", () => ({
  config: { pdmtapi: { baseUrl: "https://api.test" } },
}));

function makeResponse(status: number, body?: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response;
}

// ─── apiFetch ────────────────────────────────────────────────────────────────

describe("apiFetch", () => {
  const fetchMock = vi.fn();
  let currentToken: string | null;
  let setTokenFn: (t: string) => void;
  let onLogoutFn: () => void;

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    currentToken = null;
    setTokenFn = vi.fn((t: string) => {
      currentToken = t;
    });
    onLogoutFn = vi.fn();
    initApiClient(() => currentToken, setTokenFn, onLogoutFn);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("sets Content-Type: application/json on every request", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await apiFetch("/api/test");
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers;
    expect(headers.get("Content-Type")).toBe("application/json");
  });

  it("sets Authorization: Bearer header when token is present", async () => {
    currentToken = "my-token";
    fetchMock.mockResolvedValue(makeResponse(200));
    await apiFetch("/api/test");
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers;
    expect(headers.get("Authorization")).toBe("Bearer my-token");
  });

  it("omits Authorization header when no token", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await apiFetch("/api/test");
    const headers = (fetchMock.mock.calls[0][1] as RequestInit).headers as Headers;
    expect(headers.get("Authorization")).toBeNull();
  });

  it("prepends base URL for relative paths", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await apiFetch("/api/events");
    expect(fetchMock.mock.calls[0][0]).toBe("https://api.test/api/events");
  });

  it("uses absolute URL as-is", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await apiFetch("https://other.host/resource");
    expect(fetchMock.mock.calls[0][0]).toBe("https://other.host/resource");
  });

  it("returns response for 2xx without retrying", async () => {
    const res = makeResponse(200);
    fetchMock.mockResolvedValue(res);
    const result = await apiFetch("/api/test");
    expect(result).toBe(res);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it("returns 500 without retry or logout", async () => {
    fetchMock.mockResolvedValue(makeResponse(500));
    const result = await apiFetch("/api/test");
    expect(result.status).toBe(500);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(onLogoutFn).not.toHaveBeenCalled();
  });

  // ─── 401 silent-refresh flow ────────────────────────────────────────────────

  it("retries with new token after successful refresh", async () => {
    const retryResponse = makeResponse(200);
    fetchMock
      .mockResolvedValueOnce(makeResponse(401)) // original request
      .mockResolvedValueOnce(makeResponse(200, { accessToken: "new-tok" })) // refresh
      .mockResolvedValueOnce(retryResponse); // retry

    const result = await apiFetch("/api/events");

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(currentToken).toBe("new-tok");
    expect(result).toBe(retryResponse);

    const retryHeaders = (fetchMock.mock.calls[2][1] as RequestInit).headers as Headers;
    expect(retryHeaders.get("Authorization")).toBe("Bearer new-tok");
  });

  it("calls onLogout and returns 401 when refresh responds with non-ok", async () => {
    fetchMock
      .mockResolvedValueOnce(makeResponse(401)) // original
      .mockResolvedValueOnce(makeResponse(401)); // refresh fails

    const result = await apiFetch("/api/events");

    expect(onLogoutFn).toHaveBeenCalledOnce();
    expect(result.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("calls onLogout when refresh request throws a network error", async () => {
    fetchMock
      .mockResolvedValueOnce(makeResponse(401))
      .mockRejectedValueOnce(new Error("Network error"));

    await apiFetch("/api/events");

    expect(onLogoutFn).toHaveBeenCalledOnce();
  });
});

// ─── apiGet ──────────────────────────────────────────────────────────────────

describe("apiGet", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    initApiClient(() => null, vi.fn(), vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("returns parsed JSON on ok response", async () => {
    fetchMock.mockResolvedValue(makeResponse(200, { id: "1" }));
    const data = await apiGet<{ id: string }>("/api/events/1");
    expect(data).toEqual({ id: "1" });
  });

  it("throws with path and status on non-ok response", async () => {
    fetchMock.mockResolvedValue(makeResponse(404));
    await expect(apiGet("/api/events/missing")).rejects.toThrow("GET /api/events/missing → 404");
  });
});

// ─── apiPost ─────────────────────────────────────────────────────────────────

describe("apiPost", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    initApiClient(() => null, vi.fn(), vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("sends serialized JSON body", async () => {
    fetchMock.mockResolvedValue(makeResponse(200, { id: "new" }));
    await apiPost("/api/events", { title: "Test" });
    expect((fetchMock.mock.calls[0][1] as RequestInit).body).toBe(
      JSON.stringify({ title: "Test" }),
    );
  });

  it("sends no body when body argument is omitted", async () => {
    fetchMock.mockResolvedValue(makeResponse(200, {}));
    await apiPost("/api/events");
    expect((fetchMock.mock.calls[0][1] as RequestInit).body).toBeUndefined();
  });

  it("returns undefined on 204 No Content without reading body", async () => {
    fetchMock.mockResolvedValue(makeResponse(204));
    const result = await apiPost<undefined>("/api/action");
    expect(result).toBeUndefined();
  });

  it("throws with path and status on non-ok response", async () => {
    fetchMock.mockResolvedValue(makeResponse(400));
    await expect(apiPost("/api/events", {})).rejects.toThrow("POST /api/events → 400");
  });
});

// ─── apiPut / apiDelete ───────────────────────────────────────────────────────

describe("apiPut", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    initApiClient(() => null, vi.fn(), vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("resolves without value on success", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await expect(apiPut("/api/events/1", {})).resolves.toBeUndefined();
  });

  it("throws with path and status on non-ok response", async () => {
    fetchMock.mockResolvedValue(makeResponse(404));
    await expect(apiPut("/api/events/1", {})).rejects.toThrow("PUT /api/events/1 → 404");
  });
});

describe("apiDelete", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    initApiClient(() => null, vi.fn(), vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("resolves without value on success", async () => {
    fetchMock.mockResolvedValue(makeResponse(200));
    await expect(apiDelete("/api/events/1")).resolves.toBeUndefined();
  });

  it("throws with path and status on non-ok response", async () => {
    fetchMock.mockResolvedValue(makeResponse(404));
    await expect(apiDelete("/api/events/1")).rejects.toThrow("DELETE /api/events/1 → 404");
  });
});
