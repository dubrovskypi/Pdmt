import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

// Combines class names and resolves Tailwind conflicts (e.g. p-2 + p-4 → p-4).
// Used by all Shadcn/ui components.
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function isAbortError(err: unknown): boolean {
  return err instanceof DOMException && err.name === "AbortError";
}

// Parses HTTP status from error messages like "GET /path → 404"
export function getErrorMessage(err: unknown): string {
  const msg = err instanceof Error ? err.message : "";
  const match = /→ (\d+)/.exec(msg);
  const status = match ? parseInt(match[1]) : 0;

  if (status === 403) return "Нет доступа к данным.";
  if (status === 404) return "Данные не найдены.";
  if (status === 429) return "Слишком много запросов. Повторите позже.";
  if (status >= 500) return "Ошибка сервера. Попробуйте позже.";
  return "Не удалось загрузить данные.";
}
