# React + Vite SPA — Feature-Sliced Architecture Guide

## Philosophy

Organize by **feature**, not by technical role. Each feature maps to a single user-facing capability (e.g., "create user", "product list") and owns its own components, hooks, API calls, validation, and types.

- A developer working on "edit product" never touches "user list" files
- Each feature can be lazy-loaded for performance
- Adding a new page = adding a new folder — existing features are never modified
- Scales from 5 to 50 features without becoming a mess

> Code that changes together lives together. Shared UI primitives and utilities live outside features.

The SPA consumes a separate backend API. It never owns business logic that belongs on the server — it owns UI logic, form validation, API integration, and client-side state.

---

## Directory Structure

```
src/
├── app/
│   ├── main.tsx              # Entry point
│   ├── App.tsx               # Root component
│   ├── providers.tsx         # All context providers composed here
│   ├── router.tsx            # All route definitions (URL → feature page)
│   └── query-client.ts       # TanStack Query global defaults
│
├── features/
│   └── <feature-name>/       # One folder per user-facing operation
│       ├── types.ts          # Feature-scoped request/response types
│       ├── schema.ts         # Zod client-side validation schema
│       ├── useXxx.ts         # TanStack Query hook (mutation or query)
│       ├── XxxForm.tsx       # Form UI component
│       └── XxxPage.tsx       # Page component (route target)
│
├── infrastructure/
│   ├── api/
│   │   ├── client.ts         # Single Axios instance with interceptors
│   │   └── endpoints.ts      # All API path constants
│   ├── config/
│   │   └── env.ts            # Typed environment variables
│   └── stores/               # Zustand stores (client-only global state)
│
├── shared/
│   ├── components/           # Dumb, reusable UI primitives (Button, Input, Table)
│   ├── types/
│   │   └── api.ts            # Shared API envelope types (matches backend response shape)
│   └── utils/                # Truly shared helper functions only
│
└── tests/
    ├── setup.ts
    ├── mocks/
    │   ├── server.ts         # MSW server setup
    │   └── handlers.ts       # MSW request handlers
    └── features/             # Tests mirroring the feature structure
```

---

## Layer-by-Layer Rules

### `app/providers.tsx`
Single place to wrap the app in all context providers. Add new providers here only.

### `app/router.tsx`
All routes declared in one file. Each route points to a feature's page component. This is the single map of URL → feature.

- Use `React.lazy()` for all feature pages (code splitting)

### `app/query-client.ts`
TanStack Query global defaults configured here. Not per-feature.

---

## Feature Slice Anatomy

Each feature folder contains exactly these files:

### `types.ts` — Feature-Scoped Types
Defines the shape of data the feature sends and receives. These mirror the backend's request/response schemas but are owned by the frontend independently.

- `CreateUserRequest`, `CreateUserResponse` live in `features/users/create-user/types.ts`
- Never share types across features — they evolve independently

### `schema.ts` — Client-Side Validation (Zod)
Zod handles form validation before the request is sent. Instant UX feedback, no network round trip.

| What | Where | Why |
|------|-------|-----|
| Field format, required, length | `schema.ts` (Zod) | Instant UX feedback, no network round trip |
| Business rules (uniqueness, auth) | Backend handler | Requires DB state the frontend doesn't have |
| Final source of truth | Backend schemas | Server never trusts the client |

### `useXxx.ts` — TanStack Query Hook
Wraps the API call in a query or mutation. Handles loading, error, success states, and cache invalidation.

- Every API call goes through a `useXxx` hook
- Components **never** call `apiClient` directly

### `XxxForm.tsx` — Form UI Component
Handles form state, Zod validation, and calls the mutation hook. Displays validation errors inline.

### `XxxPage.tsx` — Page Component (Route Target)
The route target. Composes feature components and handles page-level concerns (titles, navigation on success, layout).

- Pages are thin — no API calls directly in pages, delegate to hooks

---

## Infrastructure Layer

### `api/client.ts`
Single Axios instance with base URL, default headers, and an error interceptor that transforms API error responses into a consistent format. Features never parse error responses themselves.

### `api/endpoints.ts`
All API paths as constants. When a backend route changes, update this one file only.

- Never use inline API URL strings — always use `API_ENDPOINTS` constants

### `config/env.ts`
Typed environment variables. No raw `import.meta.env` access outside this file.

---

## State Ownership

| State type | Owner | Example |
|------------|-------|---------|
| Server / API data | TanStack Query | user list, product details |
| Form input | Component `useState` | email field, name field |
| Global UI state | Zustand store | sidebar open, toast queue |
| URL state | React Router | current page, route params |

- Zustand is for **client-only** state. If data comes from the API, it belongs in TanStack Query.

---

## How to Add a New Feature

Example: adding "Create Order" (`POST /api/v1/orders`):

1. Create `src/features/orders/create-order/`
2. Add `types.ts` — define `CreateOrderRequest` and `CreateOrderResponse`
3. Add `schema.ts` — Zod schema for form validation
4. Add `useCreateOrder.ts` — TanStack Query mutation hook
5. Add `CreateOrderForm.tsx` — form component using the schema and hook
6. Add `CreateOrderPage.tsx` — page component that renders the form
7. Add the route in `app/router.tsx`
8. Add `API_ENDPOINTS.ORDERS` in `infrastructure/api/endpoints.ts` (if not already there)
9. Add test handlers in `tests/mocks/handlers.ts`
10. Add `tests/features/orders/create-order.test.tsx`

You never touch existing feature files.

---

## Best Practices Checklist

1. One feature folder = one user-facing operation (`create-user`, `user-list`, `edit-user` are separate folders)
2. Pages are thin — no API calls in pages, delegate to hooks
3. Every API call goes through a `useXxx` hook wrapping TanStack Query
4. Zod validates forms client-side for UX — backend is always the source of truth
5. Types are feature-scoped — keep them separate even if they look similar
6. Shared components are dumb — props in, render out; no API calls, no business logic
7. Zustand is for client-only state — API data belongs in TanStack Query
8. All API paths live in `endpoints.ts`
9. Axios interceptors handle error transformation globally
10. Tests use MSW to mock the API — no real network calls
11. Lazy-load all feature pages via `React.lazy()` in the router
12. Never import from one feature into another — if two features share something, it belongs in `shared/`

---

## Anti-Patterns to Avoid

- API calls directly in components — always go through a `useXxx` hook
- Business logic on the frontend — form validation for UX is fine; business rules belong on the backend
- Shared types across features — keep them separate, they evolve independently
- Giant `utils/` folder — feature-specific utilities stay in the feature folder
- Zustand for server data — use TanStack Query instead
- Cross-feature imports — if a feature needs data from another domain, call the API via its own hook
- Inline API URL strings — always use `API_ENDPOINTS` constants
