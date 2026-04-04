// API client with automatic Bearer injection and 401 → silent refresh → retry.
// Mirrors the pattern from Pdmt.Client/Services/AuthHeaderHandler.cs.

import { config } from "@/config";

type TokenGetter = () => string | null;
type TokenSetter = (token: string) => void;
type LogoutCallback = () => void;

let getToken: TokenGetter = () => null;
let setToken: TokenSetter = () => {};
let onLogout: LogoutCallback = () => {};

// Called once from AuthProvider on mount to wire up token state.
export function initApiClient(
  getter: TokenGetter,
  setter: TokenSetter,
  logout: LogoutCallback,
): void {
  getToken = getter;
  setToken = setter;
  onLogout = logout;
}

async function tryRefresh(): Promise<boolean> {
  try {
    const res = await fetch(`${config.pdmtapi.baseUrl}/api/auth/web/refresh`, {
      method: "POST",
      credentials: "include", // sends httpOnly refreshToken cookie
    });
    if (!res.ok) return false;
    const data = (await res.json()) as { accessToken: string };
    setToken(data.accessToken);
    return true;
  } catch {
    return false;
  }
}

export async function apiFetch(
  path: string,
  options: RequestInit = {},
): Promise<Response> {
  const token = getToken();
  const headers = new Headers(options.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const url = path.startsWith("http")
    ? path
    : `${config.pdmtapi.baseUrl}${path}`;
  const res = await fetch(url, { ...options, credentials: "include", headers });

  if (res.status !== 401) return res;

  // 401 — attempt silent refresh via cookie
  const refreshed = await tryRefresh();
  if (!refreshed) {
    onLogout();
    return res;
  }

  // Retry original request with new token
  const newToken = getToken();
  if (newToken) headers.set("Authorization", `Bearer ${newToken}`);
  return fetch(url, { ...options, credentials: "include", headers });
}

export async function apiGet<T>(path: string): Promise<T> {
  const res = await apiFetch(path);
  if (!res.ok) throw new Error(`GET ${path} → ${res.status}`);
  return res.json() as Promise<T>;
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  const res = await apiFetch(path, {
    method: "POST",
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`POST ${path} → ${res.status}`);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export async function apiPut(path: string, body?: unknown): Promise<void> {
  const res = await apiFetch(path, {
    method: "PUT",
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`PUT ${path} → ${res.status}`);
}

export async function apiDelete(path: string): Promise<void> {
  const res = await apiFetch(path, { method: "DELETE" });
  if (!res.ok) throw new Error(`DELETE ${path} → ${res.status}`);
}
