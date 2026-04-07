/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_PDMT_API_BASE_URL: string | undefined;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
