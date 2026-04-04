// API configuration
// In development: defaults to localhost:7031
// In production: must be set via VITE_PDMT_API_BASE_URL env var
const isDev = import.meta.env.MODE === "development";
const baseUrlDev = "https://localhost:7031";
const baseUrl =
  import.meta.env.VITE_PDMT_API_BASE_URL || (isDev ? baseUrlDev : undefined);

if (!baseUrl) {
  throw new Error(
    "VITE_PDMT_API_BASE_URL is required in production. Set it in .env or environment variables.",
  );
}

export const config = {
  pdmtapi: { baseUrl },
} as const;
