# pdmt-web

React SPA frontend for [Pdmt](../README.md) — a personal event tracking app.

## Stack

React 19 · TypeScript · Vite · Tailwind CSS · shadcn/ui · React Router v7 · Recharts

## Prerequisites

Node.js 20+

## Getting started

```bash
cp .env.example .env   # set VITE_PDMT_API_BASE_URL
npm install
npm run dev            # https://localhost:5173
```

## Commands

| Command | Description |
|---|---|
| `npm run dev` | Start dev server (HTTPS, port 5173) |
| `npm run build` | Production build → `dist/` |
| `npm run lint` | Run ESLint |
| `npm run preview` | Preview production build locally |

## Project structure

```
src/
  api/          HTTP client and endpoint modules
  auth/         Auth context, provider, hook
  components/   Shared components + shadcn/ui kit (ui/)
  hooks/        Custom hooks
  lib/          Utilities (cn, dateUtils)
  pages/        Page components
```

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `VITE_PDMT_API_BASE_URL` | prod only | Backend API base URL |

In development, defaults to `https://localhost:7031` if not set.
