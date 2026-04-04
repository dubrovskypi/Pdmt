import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

// Combines class names and resolves Tailwind conflicts (e.g. p-2 + p-4 → p-4).
// Used by all Shadcn/ui components.
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
