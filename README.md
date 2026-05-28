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

## Features

| Feature | Description |
|---------|-------------|
| Route Plotting | PostGIS spatial queries for transit route search |
| Fare Calculator | LTFRB-compliant with Haversine distance, 25-centavo rounding, discounts |
| Demand Heatmap | Real-time via WebSocket with geohash-based aggregation |
| Payment Notifications | Anti-123 system — webhook ingestion → WebSocket push |
| Authentication | Argon2id password hashing + JWT (HS256) with refresh tokens |
| Bilingual UI | English/Filipino via next-intl |
| Offline-First | PWA with IndexedDB write queue for unreliable connections |

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Backend API |
| Node.js | 18+ | Frontend PWA |
| npm | 9+ | Package management |
| Docker | Latest | PostgreSQL + DynamoDB Local |
| k6 | Latest | Load testing (optional) |

## Quick Start

### 1. Clone and set up environment variables

```bash
cd apps/web
cp .env.local.example .env.local
```

Edit `apps/web/.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_WS_URL=ws://localhost:5000/ws
```

### 2. Start infrastructure (PostgreSQL + DynamoDB Local)

```bash
docker-compose -f docker-compose.test.yml up -d
```

### 3. Run the backend API

```bash
cd apps/api
dotnet restore
dotnet build
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
│   │       ├── Infrastructure/      # DB connections, repositories, services
│   │       └── Program.cs           # Application entry point
│   │
│   └── web/
│       ├── src/
│       │   ├── app/                 # Next.js app router pages
│       │   ├── components/          # Shared UI components
│       │   ├── features/            # Feature-specific modules
│       │   ├── hooks/               # Custom React hooks
│       │   ├── lib/                 # Utilities and API client
│       │   ├── stores/              # Zustand state stores
│       │   └── messages/            # i18n translation files (en/fil)
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

| Variable | Description | Default |
|----------|-------------|---------|
| `NEXT_PUBLIC_API_URL` | Backend API base URL | `http://localhost:5000` |
| `NEXT_PUBLIC_WS_URL` | WebSocket endpoint URL | `ws://localhost:5000/ws` |

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

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, Dapper, PostgreSQL + PostGIS, DynamoDB, Argon2id, JWT |
| Frontend | Next.js 14, React 18, Tailwind CSS, Zustand, TanStack Query, Leaflet |
| PWA | next-pwa, IndexedDB (offline queue) |
| i18n | next-intl (English + Filipino) |
| Testing | xUnit + FsCheck, Vitest + Testing Library + axe-core, Playwright, k6 |
| Infrastructure | AWS CDK (Lambda, API Gateway, DynamoDB, RDS, CloudFront, SES) |

## License

MIT License. See [LICENSE](LICENSE) for details.
