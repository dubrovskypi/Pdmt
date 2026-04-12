import { apiFetch } from "./client";
import { config } from "@/config";
import type { WebAuthResultDto } from "./types";
import { WebAuthResultSchema } from "./schemas";

export async function login(email: string, password: string): Promise<WebAuthResultDto> {
  const res = await fetch(`${config.pdmtapi.baseUrl}/api/auth/web/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error(`Login failed: ${res.status}`);
  return WebAuthResultSchema.parse(await res.json()) as WebAuthResultDto;
}

export async function register(email: string, password: string): Promise<WebAuthResultDto> {
  const res = await fetch(`${config.pdmtapi.baseUrl}/api/auth/web/register`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error(`Register failed: ${res.status}`);
  return WebAuthResultSchema.parse(await res.json()) as WebAuthResultDto;
}

// Called on page load to silently restore session from httpOnly cookie.
export async function refreshSilent(): Promise<WebAuthResultDto | null> {
  try {
    const res = await fetch(`${config.pdmtapi.baseUrl}/api/auth/web/refresh`, {
      method: "POST",
      credentials: "include",
    });
    if (!res.ok) return null;
    return WebAuthResultSchema.parse(await res.json()) as WebAuthResultDto;
  } catch {
    return null;
  }
}

export async function logout(): Promise<void> {
  await apiFetch("/api/auth/web/logout", { method: "POST" });
}
