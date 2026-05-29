# BiyaHero 🇵🇭

A Philippine transit app MVP — route plotting, fare calculation, real-time demand heatmaps, and payment notifications for commuters navigating Philippine public transport.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Client (PWA)                          │
│              Next.js 14 · React 18 · Tailwind CSS            │
│         Leaflet Maps · Zustand · TanStack Query · next-pwa   │
└────────────────────┬───────────────────┬────────────────────┘
                     │ REST              │ WebSocket
                     ▼                   ▼
┌─────────────────────────────────────────────────────────────┐
│                      API Gateway                             │
│                .NET 8 Web API (Lambda)                        │
│         Feature-Sliced · Dapper · Argon2id · JWT             │
└──────┬──────────────┬──────────────┬────────────────────────┘
       │              │              │
       ▼              ▼              ▼
┌────────────┐ ┌────────────┐ ┌────────────┐
│ PostgreSQL │ │  DynamoDB  │ │    SES     │
│ + PostGIS  │ │  (Demand)  │ │  (Email)   │
└────────────┘ └────────────┘ └────────────┘
```

```
apps/
├── api/          .NET 8 Web API — feature-sliced architecture
└── web/          Next.js 14 PWA — TypeScript + Tailwind CSS
infra/            AWS CDK (C#) infrastructure-as-code
tests/load/       k6 load test scripts
```

## Routing & Navigation

After login, users are redirected to their role-specific dashboard:

| Role | Dashboard URL | Description |
|------|--------------|-------------|
| `commuter` | `/commuter/dashboard` | Mobile-first PWA shell with bottom nav (Home, Routes, Fare, Profile) |
| `driver` | `/driver/dashboard` | Full-screen interactive heatmap with bottom-sheet tabs (Payments, Routes, Profile) |
| `moderator` / `super_admin` | `/admin/users` | Admin user management panel |
| Guest | `/` | Landing page with feature highlights and sign-up CTA |

### Commuter Dashboard (`/commuter/dashboard`)

Fixed-height PWA shell — the bottom nav never scrolls away. Tabs switch content in-page:
- **Home** — "I'm Waiting Here" primary CTA + Fare Calculator and Plot Route quick actions
- **Routes** — links to browse and plot community routes
- **Fare** — inline summary + link to the full fare calculator
- **Profile** — avatar, account info, language, and logout

### Driver Dashboard (`/driver/dashboard`)

Full-screen interactive heatmap as the primary view. Overlays never block map interaction:
- **Heatmap tab** — live demand tiles, legend positioned above the bottom nav
- **Payments tab** — bottom sheet with payment alert info and history
- **Routes tab** — bottom sheet with links to browse/plot routes
- **Profile tab** — bottom sheet with account info and logout

## Features

| Feature | Description |
|---------|-------------|
| Route Plotting | PostGIS spatial queries for transit route search |
| Fare Calculator | LTFRB-compliant with Haversine distance, 25-centavo rounding, discounts |
| Demand Heatmap | Real-time via WebSocket with geohash-based aggregation |
| Payment Notifications | Anti-123 system — webhook ingestion → WebSocket push |
| Authentication | Argon2id password hashing + JWT (HS256) with refresh tokens |
| Role-Based Dashboards | Commuter and Driver each get a dedicated mobile-first PWA shell |
| Bilingual UI | English/Filipino via next-intl |
| Offline-First | PWA with IndexedDB write queue for unreliable connections |

## One-Command Start

Two PowerShell scripts handle the entire local stack — infrastructure, API, and frontend.

```powershell
# Start everything (.NET API + Next.js PWA, + Docker infra if available)
.\start.ps1

# Stop everything cleanly
.\stop.ps1
```

| URL | Service |
|-----|---------|
| `http://localhost:3000` | Next.js PWA (frontend) |
| `http://localhost:5000` | .NET 8 API (backend) |
| `http://localhost:8000` | DynamoDB Local (requires Docker) |
| `localhost:5432` | PostgreSQL + PostGIS (requires Docker) |

What `start.ps1` does:
1. Checks that .NET SDK, Node.js, and npm are installed — prints install links for anything missing
2. **If Docker is available:** starts PostgreSQL + DynamoDB Local via Docker Compose and waits for them to be healthy, then injects the correct DB credentials automatically
3. **If Docker is not installed:** starts the API and frontend anyway, and prints instructions for installing Docker Desktop or native PostgreSQL
4. Starts the .NET API in a new window
5. Installs frontend `node_modules` if not already present, then starts Next.js in a new window
6. Prints a summary with all service URLs

`stop.ps1` reads the PIDs saved by `start.ps1` and shuts down the API, frontend, and Docker containers in one step.

> **Windows only.** On macOS/Linux, use the manual steps in the [Quick Start](#quick-start) section below.

### No Docker? No problem

`start.ps1` will still launch the API and frontend even without Docker. Database-backed features (login, register, routes, heatmap) won't work until you add a database, but the UI is fully browsable. To get the full stack running without Docker, install PostgreSQL natively:

1. Download from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/)
2. Create a database named `biyahero` with user `postgres` / password `postgres`
3. Re-run `.\start.ps1` — the API will connect automatically

---

## Prerequisites

| Tool | Version | Required | Purpose |
|------|---------|----------|---------|
| .NET SDK | 8.0+ | ✅ | Backend API |
| Node.js | 18+ | ✅ | Frontend PWA |
| npm | 9+ | ✅ | Package management |
| Docker Desktop | Latest | ⚠️ Recommended | PostgreSQL + DynamoDB Local (see [No Docker?](#no-docker-no-problem)) |
| k6 | Latest | ❌ Optional | Load testing |

## Quick Start

> **Recommended:** Use `.\start.ps1` from the [One-Command Start](#one-command-start) section above — it handles all steps automatically.

The manual steps below are for macOS/Linux or if you prefer to run each service individually.

### 1. Set up environment variables (optional)

The frontend defaults to `http://localhost:5000` for the API and `ws://localhost:5000/ws` for WebSockets. A `.env.local` file is only needed if your backend runs on a different host or port.

```bash
cd apps/web
cp .env.local.example .env.local   # optional — defaults work out of the box
```

If you need to override the defaults, edit `apps/web/.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_WS_URL=ws://localhost:5000/ws
```

### 2. Start infrastructure (PostgreSQL + DynamoDB Local)

```bash
docker compose -f docker-compose.test.yml up -d
```

The Docker Compose file uses `biyahero_test`/`test_password` as the PostgreSQL credentials. Pass them to the API via environment variables so no config file edits are needed:

```bash
# macOS / Linux
export ConnectionStrings__Postgres="Host=localhost;Database=biyahero_test;Username=biyahero_test;Password=test_password"
export ConnectionStrings__DynamoDB="http://localhost:8000"
export ASPNETCORE_ENVIRONMENT=Development
```

```powershell
# Windows PowerShell
$env:ConnectionStrings__Postgres = "Host=localhost;Database=biyahero_test;Username=biyahero_test;Password=test_password"
$env:ConnectionStrings__DynamoDB = "http://localhost:8000"
$env:ASPNETCORE_ENVIRONMENT      = "Development"
```

### 3. Run the backend API

```bash
cd apps/api
dotnet run --project BiyaHero.Api
```

API available at `http://localhost:5000`

### 4. Run the frontend PWA

```bash
cd apps/web
npm install
npm run dev
```

PWA available at `http://localhost:3000`

## Available Scripts

### Backend (`apps/api/`)

| Command | Description |
|---------|-------------|
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the solution |
| `dotnet run --project BiyaHero.Api` | Run the API server |
| `dotnet test` | Run all backend tests |

### Frontend (`apps/web/`)

| Command | Description |
|---------|-------------|
| `npm run dev` | Start development server |
| `npm run build` | Production build |
| `npm run start` | Start production server |
| `npm test` | Run unit/integration tests (Vitest) |
| `npm run test:e2e` | Run Playwright E2E tests |
| `npm run lint` | Lint with ESLint |

### Infrastructure

| Command | Description |
|---------|-------------|
| `docker-compose -f docker-compose.test.yml up -d` | Start local DB services |
| `docker-compose -f docker-compose.test.yml down` | Stop local DB services |

### Load Tests (`tests/load/`)

| Command | Description |
|---------|-------------|
| `k6 run rest-mixed-workload.js` | Mixed REST API load test |
| `k6 run ws-fanout.js` | WebSocket fanout stress test |
| `k6 run heatmap-aggregation.js` | Heatmap aggregation load test |

## Project Structure

```
.
├── apps/
│   ├── api/
│   │   └── BiyaHero.Api/
│   │       ├── Domain/              # Domain models and value objects
│   │       ├── Features/            # Feature-sliced endpoints
│   │       │   ├── Admin/           # User management (list, promote, suspend)
│   │       │   ├── Auth/            # Register, login, logout, refresh, verify
│   │       │   ├── Common/          # Middleware, exceptions, monitoring
│   │       │   ├── Fare/            # Fare calculation endpoints
│   │       │   ├── Heatmap/         # Demand heatmap (REST + WebSocket)
│   │       │   ├── Payments/        # Payment webhook + notifications
│   │       │   └── Routes/          # Route CRUD + spatial search
│   │       ├── WebSockets/          # Local WS endpoint (dev), route/demand handlers
│   │       ├── Infrastructure/      # DB connections, repositories, services
│   │       └── Program.cs           # Application entry point
│   │
│   └── web/
│       ├── app/                     # Next.js App Router pages
│       │   ├── commuter/
│       │   │   ├── dashboard/       # Commuter PWA home shell
│       │   │   ├── fare/            # Fare calculator page
│       │   │   ├── routes/          # Route list + create pages
│       │   │   └── waiting/         # "I'm Waiting Here" page
│       │   ├── driver/
│       │   │   ├── dashboard/       # Driver PWA home shell (full-screen heatmap)
│       │   │   └── heatmap/         # Standalone heatmap page
│       │   ├── admin/               # Admin user management
│       │   └── (auth)/              # Login, register, verify pages
│       ├── features/                # Feature-sliced modules
│       │   ├── auth/                # Login, register, useMe, useLogout
│       │   ├── dashboard/
│       │   │   ├── commuter/        # CommuterDashboard component
│       │   │   └── driver/          # DriverDashboard + DriverHeatmapMap
│       │   ├── fare/                # Fare calculator feature
│       │   ├── heatmap/
│       │   │   ├── commuter-heatmap/ # Commuter waiting/ping feature
│       │   │   └── driver-heatmap/   # Driver heatmap tiles + WebSocket hook
│       │   ├── routes/              # Route list + create features
│       │   ├── admin/               # Admin user list, suspend, promote
│       │   └── payment/             # Payment alert feature
│       ├── infrastructure/          # API client, stores, config, offline queue
│       ├── shared/                  # Reusable UI primitives (Button, ThemeToggle…)
│       ├── public/                  # Static assets + PWA manifest
│       └── tests/                   # Vitest + Playwright tests
│
├── infra/                           # AWS CDK infrastructure (C#)
│   ├── Stacks/                      # CDK stack definitions
│   └── Program.cs                   # CDK app entry point
│
├── tests/
│   └── load/                        # k6 load test scripts
│       ├── rest-mixed-workload.js   # REST endpoint stress test
│       ├── ws-fanout.js             # WebSocket broadcast test
│       └── heatmap-aggregation.js   # Heatmap performance test
│
└── docker-compose.test.yml          # Local dev infrastructure
```

## Testing

### Backend (xUnit + FsCheck)

Property-based testing with FsCheck ensures domain logic correctness:

```bash
cd apps/api
dotnet test
```

Key test areas:
- Fare calculation properties (rounding, discount invariants)
- Route spatial query correctness
- Auth token generation and validation

### Frontend (Vitest + Testing Library + axe-core)

```bash
cd apps/web
npm test
```

Includes accessibility testing via axe-core for WCAG compliance.

### End-to-End (Playwright)

```bash
cd apps/web
npm run build
npm run test:e2e
```

Requires a production build and running backend.

### Load Tests (k6)

```bash
cd tests/load
k6 run rest-mixed-workload.js
k6 run ws-fanout.js
k6 run heatmap-aggregation.js
```

## Environment Variables

### Frontend (`apps/web/.env.local`)

| Variable | Description | Default (used when unset) |
|----------|-------------|---------------------------|
| `NEXT_PUBLIC_API_URL` | Backend API base URL | `http://localhost:5000` |
| `NEXT_PUBLIC_WS_URL` | WebSocket endpoint URL | `ws://localhost:5000/ws` |

> **Note:** The frontend will start without a `.env.local` file — it falls back to the defaults above automatically.

### Backend (`apps/api/BiyaHero.Api/appsettings.Development.json`)

| Setting | Description |
|---------|-------------|
| `ConnectionStrings:Postgres` | PostgreSQL + PostGIS connection string |
| `ConnectionStrings:DynamoDB` | DynamoDB Local endpoint |
| `Jwt:Secret` | HS256 signing key |
| `Jwt:Issuer` | Token issuer |
| `Jwt:ExpiryMinutes` | Access token TTL |
| `Ses:FromAddress` | Email sender address |

## Tech Stack

### Backend

| Technology | Purpose |
|------------|---------|
| **.NET 8** | Web API runtime. Chosen for high throughput, minimal overhead, and first-class async support — critical for WebSocket fan-out and concurrent REST requests. |
| **Feature-Sliced Architecture** | Each API feature (Register, Login, Fare, Heatmap, etc.) lives in its own folder with its own endpoint, handler, and request/response types. Adding a feature never touches existing code. |
| **Dapper** | Lightweight micro-ORM for raw SQL over PostgreSQL. Gives full control over spatial queries (PostGIS) without the overhead of EF Core's change tracker. |
| **PostgreSQL + PostGIS** | Relational store for users, routes, and waypoints. PostGIS extension enables native geospatial queries — bounding-box route search and Haversine distance calculation run as single SQL statements. |
| **DynamoDB** | NoSQL store for demand-ping events and heatmap tiles. Chosen for its sub-millisecond write latency and pay-per-request pricing, which fits the 500k pings/month Free Tier budget. |
| **Argon2id** | Password hashing algorithm. Memory-hard by design, making brute-force and GPU attacks impractical. The current OWASP-recommended choice over bcrypt and scrypt. |
| **JWT (HS256)** | Stateless access tokens for API authentication. Short-lived access tokens + longer-lived refresh tokens stored in HttpOnly cookies prevent XSS token theft. |
| **AWS SES** | Transactional email for account verification. Serverless, pay-per-email, and integrates directly with Lambda — no SMTP server to manage. |
| **AWS Lambda** | Serverless compute for the API. Scales to zero when idle (Free Tier friendly) and scales out automatically under load without provisioning servers. |
| **AWS API Gateway** | Managed HTTP and WebSocket gateway in front of Lambda. Handles TLS termination, throttling, and WebSocket connection management so the application code doesn't have to. |

### Frontend

| Technology | Purpose |
|------------|---------|
| **Next.js 14** | React framework with App Router. Provides file-based routing, server components for fast initial loads, and built-in image/font optimisation. |
| **React 18** | UI library. Concurrent rendering and Suspense boundaries keep the map and data-heavy pages responsive during async operations. |
| **TypeScript** | Static typing across the entire frontend. Catches API contract mismatches at compile time rather than at runtime in production. |
| **Tailwind CSS** | Utility-first CSS framework. Enables rapid UI iteration without context-switching to separate stylesheets. All styles are co-located with components and tree-shaken in production. |
| **Dark / Light / System Theme** | Three-way theme toggle (light → dark → system) powered by a Zustand store, `localStorage` persistence, and Tailwind's `darkMode: "class"` strategy. Respects OS preference automatically in system mode. |
| **Zustand** | Minimal global state manager. Used for client-only state (theme preference, language preference, toast queue) that doesn't belong in the server cache. Avoids the boilerplate of Redux for small shared state. |
| **TanStack Query (React Query)** | Server state management. Handles caching, background refetching, optimistic updates, and loading/error states for all API calls. Components never call `fetch` directly. |
| **Leaflet + React-Leaflet** | Interactive maps for route plotting, fare pin picking, and the demand heatmap. Open-source, mobile-friendly, and works offline with cached tiles. |
| **Zod** | Runtime schema validation for all form inputs. Provides instant client-side feedback before a network request is made, while the backend remains the authoritative source of truth. |
| **next-intl** | Internationalisation for English and Filipino (Tagalog). Translation keys are type-safe and co-located with the feature that uses them. |
| **next-pwa** | Service Worker generation for offline support. Caches static assets and API responses so the app remains usable on flaky Philippine mobile connections. |
| **IndexedDB (offline queue)** | Persists form submissions (demand pings, route plots) locally when the device is offline. The queue drains automatically when connectivity is restored. |
| **Axios** | HTTP client with a single configured instance. Interceptors handle auth token injection, 401 refresh-token rotation, and consistent error shape transformation globally. |

### Testing

| Technology | Purpose |
|------------|---------|
| **xUnit** | Primary backend test runner. Integrates cleanly with .NET's built-in DI and supports parallel test execution. |
| **FsCheck** | Property-based testing for the backend. Generates hundreds of random inputs to verify fare calculation invariants (rounding, discount bounds, distance edge cases) that hand-written examples would miss. |
| **Vitest** | Frontend unit and integration test runner. Vite-native, so it shares the same module resolution and TypeScript config as the app — no separate Babel setup. |
| **Testing Library** | Component testing utilities. Encourages testing from the user's perspective (by role, label, text) rather than implementation details. |
| **axe-core** | Automated accessibility auditing integrated into Vitest tests. Catches WCAG 2.1 AA violations (missing labels, contrast failures, focus traps) on every component render. |
| **Playwright** | End-to-end browser testing. Runs full user journeys (register → login → plot route → calculate fare) against a real browser and running backend. |
| **k6** | Load testing for performance requirements. Three scripts validate: REST p95 ≤ 400ms under 50 VUs, WebSocket fan-out with 200 concurrent connections and zero dropped messages, and heatmap aggregation p95 ≤ 500ms. |
| **MSW (Mock Service Worker)** | API mocking in frontend tests. Intercepts real `fetch`/`axios` calls at the network level so tests exercise the full request/response cycle without a running backend. |

### Infrastructure

| Technology | Purpose |
|------------|---------|
| **AWS CDK (C#)** | Infrastructure-as-code using the same language as the backend. Defines Lambda functions, API Gateway, DynamoDB tables, RDS (PostgreSQL), CloudFront distribution, and SES configuration as typed C# constructs. |
| **Amazon CloudFront** | CDN for the Next.js PWA static assets. Serves the app from edge locations closest to Philippine users, reducing latency for the initial page load. |
| **Amazon RDS (PostgreSQL)** | Managed PostgreSQL with PostGIS. Handles automated backups, minor version patching, and Multi-AZ failover without manual DBA work. |
| **Docker Compose** | Local development infrastructure. Spins up PostgreSQL + PostGIS and DynamoDB Local in containers so developers can run the full stack without cloud credentials. |

## Known Issues & Recent Fixes

### Heatmap signal not appearing on driver map (fixed)
`LocalWebSocketEndpoint.BuildHeatmapTiles()` was emitting tiles with a `vehicleTypes` array and a fake string key instead of the shape the frontend expected (`{ geohash7, demandCount, vehicleType }`). Fixed to use `GeohashEncoder.EncodeForTile()` and group by `(geohash7, vehicleType)`.

### Login always redirected to `/` regardless of role (fixed)
`useLogin` now returns the normalized role after a successful login. `LoginPage` uses a `getRoleRedirect()` function to send drivers to `/driver/dashboard`, admins to `/admin/users`, and commuters to `/commuter/dashboard`.

### Login page redesign
Split-panel layout: brand hero on the left (desktop), clean form on the right. Replaces the previous homepage-style layout.

### Commuter waiting page missing back button (fixed)
Added a `←` back-to-home button in the header, consistent with all other feature pages.

## License

MIT License. See [LICENSE](LICENSE) for details.
