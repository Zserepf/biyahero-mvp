# Implementation Plan: BiyaHero MVP

## Overview

Convert the feature design into a series of prompts for a code-generation LLM that will implement each step with incremental progress. Make sure that each prompt builds on the previous prompts, and ends with wiring things together. There should be no hanging or orphaned code that isn't integrated into a previous step. Focus ONLY on tasks that involve writing, modifying, or testing code.

The plan slices the work along the four MVP problem domains (Routing, Fare, Anti-123 Payment, Heatmap) plus cross-cutting concerns (Auth, PWA shell, Localization, AWS infra). Backend lives in `apps/api` as a feature-sliced C# .NET 8 solution per the OOP steering (`Domain` → `Repositories` → `Services`); frontend lives in `apps/web` as a feature-sliced Next.js + Tailwind PWA per the React/Vite SPA steering. AWS deployment is defined in `infra/BiyaHero.Infra` as a C# CDK app. Property-based tests use FsCheck, unit tests use xUnit, frontend tests use Vitest + Testing Library + axe, end-to-end PWA scenarios use Playwright, and load tests use k6.

## Tasks

- [x] 1. Bootstrap monorepo and per-app scaffolds
  - [x] 1.1 Initialize `apps/web` Next.js 14 + TypeScript + Tailwind + next-pwa scaffold
    - Create `apps/web` with App Router, Tailwind config, ESLint + Prettier
    - Add `next-pwa` dependency and base `next.config.js` (i18n, headers, asset prefix)
    - Add empty `apps/web/locales/en.json` and `apps/web/locales/fil.json` placeholders
    - _Requirements: 6.1, 6.2, 10.1, 10.7, 10.8_

  - [x] 1.2 Initialize `apps/api` .NET 8 solution with feature-sliced layout
    - Create `BiyaHero.Api` Web API project + `BiyaHero.Api.Tests` xUnit project + FsCheck reference
    - Create empty folders: `Features/{Auth,Routing,Fare,Payment,Heatmap,Common}`, `Domain`, `Repositories`, `Services`, `WebSockets`
    - Configure native AOT publish profile and ARM64 Linux target
    - _Requirements: 7.1, 7.2_

  - [x] 1.3 Initialize `infra/BiyaHero.Infra` AWS CDK (C#) project
    - Create empty `ApiStack`, `DataStack`, `FrontendStack`, `MonitoringStack` skeletons
    - Add `cdk.json` + bootstrap script targeting a free-tier-eligible region
    - _Requirements: 7.1, 7.4_

  - [x] 1.4 Add `docker-compose.test.yml` test harness
    - Define services for PostgreSQL 16 with PostGIS and DynamoDB Local
    - Add a make/script target for booting the harness for integration tests
    - _Requirements: 1.2, 4.1, 4.2_

- [x] 2. Shared backend foundations
  - [x] 2.1 Implement `BaseDomain` abstract class
    - Provide `Find`, `FindAll`, `Create`, `Where` static class methods and `Save`, `Update`, `Delete`, `Serialize` instance methods per the OOP steering
    - Hold `Id`, `CreatedAt`, `UpdatedAt` on every entity
    - _Requirements: 1.9, 1.10, 3.10_

  - [x] 2.2 Implement `IRepository<T>` and `BasePostgresRepository<T>` using Dapper (AOT-safe)
    - Generic CRUD + transactional helpers; never expose SQL strings to handlers
    - _Requirements: 1.1, 1.2, 5.1, 5.7_

  - [x] 2.3 Implement `BaseDynamoRepository<T>`
    - Generic `GetItem`, `PutItem` (with `attribute_not_exists` conditional), `Query`, `Delete`; abstract over partition + sort key encoding
    - _Requirements: 3.7, 4.1, 4.4_

  - [x] 2.4 Implement REST error envelope and global exception middleware
    - Translate typed exceptions to `{ error: { code, message, details } }` per design Error Handling table (400, 401, 403, 404, 409, 422, 500)
    - _Requirements: 1.6, 1.7, 2.6, 2.7, 5.5, 5.6, 5.8_

  - [x] 2.5 Implement WebSocket envelope helpers and 4001 close-code utility
    - Standard envelope `{ action, requestId, data, emittedAt? }`
    - Force-close socket on auth failure even when the close-frame fails to flush
    - _Requirements: 4.7, 5.4_

  - [x] 2.6 Implement KMS-backed secret service with in-process cache
    - Cache JWT signing key + webhook signing secret to avoid blowing the 20k req/month KMS free tier
    - _Requirements: 5.3, 5.4, 5.7, 7.4_

  - [x] 2.7 Write unit tests for `BaseDomain.Serialize` round-trip and base repositories
    - Stubbed in-memory store; verifies Serialize → Parse equivalence for a placeholder entity
    - _Requirements: 1.11, 3.11_

- [x] 3. Database schema (Postgres migrations and seeds)
  - [x] 3.1 Migration: enums, `users`, `audit_log`, indexes
    - `user_role`, `user_status`, `vehicle_type`, `route_status`, `revision_status`, `vote_kind` enums
    - `users` table with `language_preference char(3) CHECK in ('en','fil') DEFAULT 'fil'`
    - `audit_log` table for Super Admin + Auth + Payment writes
    - _Requirements: 5.1, 5.7, 5.10, 8.5, 10.1_

  - [x] 3.2 Migration: `routes` and `route_waypoints` with PostGIS
    - Enable `postgis` extension; `route_waypoints.location geometry(Point, 4326)` with GiST index
    - Document the lat/lng `double precision` B-tree fallback and provide it as a conditional path
    - _Requirements: 1.1, 1.2, 1.7, 1.8_

  - [x] 3.3 Migration: `route_revisions`, `route_revision_waypoints`, `route_votes`
    - One vote per user per route via `UNIQUE (route_id, voter_id)`
    - _Requirements: 1.3, 1.4, 1.5_

  - [x] 3.4 Seed: LTFRB fare matrix versioned config
    - Create `fare_matrices` table with `version`, `effective_at`, `vehicle_type`, `min_fare_centavos`, `min_fare_km`, `per_km_centavos`, `discount_percent_by_category`
    - Seed an initial `v1` matrix matching the LTFRB-published values
    - _Requirements: 2.1, 2.3, 2.4, 2.5, 2.8_

- [x] 4. Auth subsystem (`Auth_Service`)
  - [x] 4.1 Implement `User` domain class
    - Inherits `BaseDomain`; properties `Email`, `PasswordHash`, `Role`, `Status`, `DisplayName`, `LanguagePreference`
    - _Requirements: 5.1, 5.2, 10.1, 10.3_

  - [x] 4.2 Implement `UserRepository`
    - Find by email (case-insensitive via citext), update language preference, suspend, change role
    - _Requirements: 5.1, 5.5, 5.8, 5.9, 10.3_

  - [x] 4.3 Implement `Argon2idPasswordHasher` service
    - Memory cost ≥ 64 MiB, iterations ≥ 3, parallelism ≥ 1
    - _Requirements: 5.7_

  - [x] 4.4 Implement JWT issuer/verifier (HS256, KMS-backed)
    - 24h access token + 30d refresh token; cached signing key per task 2.6
    - _Requirements: 5.3, 5.4, 5.7_

  - [x] 4.5 Implement `POST /v1/auth/registrations` + SES verification email
    - Status `pending_verification`, single-use 24h token, opaque 409 on email conflict (no verification-status leak)
    - _Requirements: 5.1, 5.5_

  - [x] 4.6 Implement `POST /v1/auth/email-verifications/:verify`
    - Activate account on valid token, invalidate the token
    - _Requirements: 5.2_

  - [x] 4.7 Implement `POST /v1/auth/sessions` (login)
    - Generic 401 message for both unknown email and wrong password
    - _Requirements: 5.3, 5.6_

  - [x] 4.8 Implement `POST /v1/auth/sessions/:refresh`
    - Issue new access token from a valid refresh token
    - _Requirements: 5.3_

  - [x] 4.9 Implement `DELETE /v1/auth/sessions/{id}` (logout)
    - Revoke refresh token
    - _Requirements: 5.3_

  - [x] 4.10 Implement `GET /v1/auth/me` and `PATCH /v1/auth/me/language-preference`
    - Persists `en` or `fil`; UI must apply within 100ms (frontend concern, see 12.8)
    - _Requirements: 10.3, 10.4_

  - [x] 4.11 Implement `POST /v1/i18n/missing-keys` logging endpoint
    - Append missing-translation events to CloudWatch for translator backfill
    - _Requirements: 10.6_

  - [x] 4.12 Implement Super Admin endpoints
    - `GET /v1/admin/users`, `POST /v1/admin/users/{id}/:suspend`, `POST /v1/admin/users/{id}/:promote`
    - `:promote` requires the acting Super Admin to re-enter their own password as a 2FA confirmation before role change
    - _Requirements: 5.8, 5.9, 5.11_

  - [x] 4.13 Implement audit log writer
    - Persist actor, action, target, timestamp to `audit_log` AND mirror to a CloudWatch log group with 30-day retention
    - _Requirements: 5.10, 8.5_

  - [x] 4.14 Write unit tests for Auth handlers
    - Email-conflict response opacity, generic login error, role gating on Moderator + Super Admin endpoints, audit log emission, Super Admin promotion 2FA
    - _Requirements: 5.5, 5.6, 5.8, 5.9, 5.10, 5.11_

- [x] 5. Routing subsystem (`Routing_Service`)
  - [x] 5.1 Implement `Route`, `Waypoint`, `RouteRevision`, `RouteVote` domain classes
    - Each inherits `BaseDomain`; `Route.Serialize` produces the JSON shape required by Req 1.9; `Route.Parse` is the inverse and the round-trip target for Property 1
    - _Requirements: 1.9, 1.10, 1.11_

  - [x] 5.2 Implement `RouteRepository`
    - Bbox query via `ST_Intersects` on the GiST-indexed waypoint column (lat/lng B-tree fallback path included)
    - Transactional create that writes `routes` + `route_waypoints` atomically
    - _Requirements: 1.1, 1.2, 1.7_

  - [x] 5.3 Implement `POST /v1/routes`
    - Validate ≥2 waypoints, lat 4.5°–21.5° N, lng 116°–127° E (Philippines bbox); reject 422 on failure; persist with status `unverified`
    - Reject anonymous callers with 401
    - _Requirements: 1.1, 1.6, 1.7, 1.8_

  - [x] 5.4 Implement `GET /v1/routes` (bbox query) and `GET /v1/routes/{id}`
    - Anonymous read allowed; meet p95 ≤ 800ms target via PostGIS GiST
    - _Requirements: 1.2_

  - [x] 5.5 Implement `POST /v1/routes/{id}/revisions`
    - Store as pending revision linked to the original route, never overwrite the verified version
    - _Requirements: 1.3, 1.6_

  - [x] 5.6 Implement `POST /v1/routes/{id}/revisions/{rid}/:approve`
    - Moderator-gated; replaces active route definition with the approved revision and sets status `verified`
    - _Requirements: 1.4, 5.8_

  - [x] 5.7 Implement `POST /v1/routes/{id}/votes`
    - Record `still_accurate` or `no_longer_accurate` with voter, route, timestamp; one vote per user per route
    - _Requirements: 1.5, 1.6_

  - [x] 5.8 Write FsCheck property test for Route round-trip
    - **Property 1: Round-trip Route serialization**
    - **Validates: Requirements 1.10, 1.11**
    - ≥100 iterations; tag `Feature: biyahero-mvp, Property 1: Round-trip Route serialization`

  - [x] 5.9 Write xUnit tests for Routing handlers
    - <2 waypoints rejected 422, anonymous write rejected 401, PH-wide submissions accepted (not just Metro Manila), Moderator-only revision approval, vote uniqueness
    - _Requirements: 1.3, 1.4, 1.5, 1.6, 1.7, 1.8_

- [x] 6. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Fare Calculator subsystem
  - [x] 7.1 Implement versioned `FareMatrix` loader
    - Loads the active matrix from the `fare_matrices` table (task 3.4); cached in-process; supports updating without redeploying
    - _Requirements: 2.1, 2.8_

  - [x] 7.2 Implement `FareCalculator` domain class
    - Haversine on WGS84; minimum-fare floor; per-km increment; discount percentage applied before rounding to nearest 25 centavos
    - Pure function: no I/O after matrix load (deterministic)
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.10_

  - [x] 7.3 Implement `POST /v1/fare/:calculate`
    - 422 for invalid coordinates or unsupported vehicle type with no fare in the response; 400 for malformed requests (missing fields, wrong content-type)
    - Response includes `amountPhp`, `distanceKm`, `matrixVersion`; meet p95 ≤ 200ms target
    - _Requirements: 2.1, 2.6, 2.7, 2.9_

  - [x] 7.4 Write FsCheck property test for fare determinism
    - **Property 3: Determinism of fare calculation**
    - **Validates: Requirements 2.10**
    - ≥100 iterations; tag `Feature: biyahero-mvp, Property 3: Determinism of fare calculation`

  - [x] 7.5 Write xUnit tests for Fare_Calculator
    - Min-fare floor, per-km increment with 25-centavo rounding, all four discount categories, 400 vs 422 disambiguation
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.7_

- [x] 8. Heatmap subsystem (`Heatmap_Service`)
  - [x] 8.1 Implement `DemandPing`, `HeatmapTile`, `WsConnection` domain classes
    - Each inherits `BaseDomain`; `HeatmapTile` carries only `Geohash7`, `DemandCount`, `VehicleType` (no PII)
    - _Requirements: 4.1, 4.6_

  - [x] 8.2 Implement `DemandPingRepository` (DynamoDB)
    - PK `GEOHASH#{geohash5}`, SK `PING#{commuterId}#{pingId}`, GSI `byCommuterId`, 5-minute TTL via `expiresAt`
    - _Requirements: 4.1, 4.4, 4.5_

  - [x] 8.3 Implement `WsConnectionRepository` (DynamoDB)
    - PK `USER#{userId}`, SK `CONN#{connectionId}`, GSI `byConnectionId`, 24h TTL safety net
    - _Requirements: 3.2, 3.6, 4.3, 5.4_

  - [x] 8.4 Implement geohash encoder + bbox aggregation utility
    - Encode at precision 5 for partition routing and precision 7 for response tiles
    - _Requirements: 4.2_

  - [x] 8.5 Implement `GET /v1/heatmap/tiles` (REST)
    - Bbox query, geohash7 demand counts, no PII, anonymous read allowed; meet p95 ≤ 500ms target
    - _Requirements: 4.2, 4.6_

  - [x] 8.6 Implement WebSocket `$connect` handler
    - Validate JWT in handshake, register `WsConnection`, drain `QueuedMessages` for the user (depends on task 9.3 repo)
    - 4001 close on missing/expired/invalid JWT, force-close even if the close-frame fails to flush
    - _Requirements: 3.6, 4.7, 5.4_

  - [x] 8.7 Implement WebSocket `$disconnect` handler
    - Remove the connection via the `byConnectionId` GSI
    - _Requirements: 4.3_

  - [x] 8.8 Implement WebSocket `demand-ping` route handler
    - Auth-required, validate Philippines bbox (4.5°–21.5° N, 116°–127° E), persist with TTL; reject invalid coords/vehicle without persistence
    - 4001 close for unauthenticated callers (read-only subscribe still permitted for anonymous)
    - _Requirements: 4.1, 4.7, 4.8, 4.9_

  - [x] 8.9 Implement WebSocket `cancel-demand` route handler
    - Remove the active ping for that commuter immediately
    - _Requirements: 4.5_

  - [x] 8.10 Implement WebSocket `subscribe-heatmap` route handler
    - Store bbox subscription on the connection record; anonymous subscribe allowed
    - _Requirements: 4.3, 4.7_

  - [x] 8.11 Implement EventBridge-driven heatmap aggregator Lambda
    - 5-second cadence; group active pings by geohash7 within each subscribed bbox; push deltas to subscribed drivers via `PostToConnection` (envelope `heatmap.delta`)
    - _Requirements: 4.2, 4.3, 4.6_

  - [x] 8.12 Write FsCheck property test for heatmap conservation
    - **Property 4: Conservation of heatmap aggregation**
    - **Validates: Requirements 4.10**
    - ≥100 iterations over sequences of submit/cancel/expire events within a 60-second window; tag `Feature: biyahero-mvp, Property 4: Conservation of heatmap aggregation`

  - [x] 8.13 Write xUnit + integration tests for Heatmap
    - PII never present in tile responses, 4001 close on unauthenticated `demand-ping`, anonymous read allowed, TTL-expired pings excluded from aggregations, PH-wide pings accepted
    - _Requirements: 4.4, 4.6, 4.7, 4.9_

- [x] 9. Payment subsystem (Anti-123)
  - [x] 9.1 Implement `PaymentEvent` domain class
    - Inherits `BaseDomain`; `Serialize`/`Parse` round-trip target for Property 2
    - _Requirements: 3.1, 3.10, 3.11_

  - [x] 9.2 Implement `PaymentEventRepository` (DynamoDB)
    - PK/SK both `EVENT#{eventId}`, conditional `attribute_not_exists(eventId)` write enforces idempotence; GSI `byDriverId` sorted by `occurredAt`; 90-day TTL
    - _Requirements: 3.1, 3.7, 8.2_

  - [x] 9.3 Implement `QueuedMessageRepository` (DynamoDB)
    - PK `USER#{driverId}`, SK `MSG#{occurredAt}#{eventId}`, 24h TTL; supports chronological drain via Query
    - _Requirements: 3.6_

  - [x] 9.4 Implement `IWalletAdapter` interface and `MockWalletAdapter`
    - Adapter is the only seam between Payment_Service and any wallet provider; mocked end-to-end for MVP, swappable post-MVP without changing the WebSocket flow or `PaymentEvent` schema
    - _Requirements: 3.9_

  - [x] 9.5 Implement HMAC-SHA256 webhook signature verifier
    - Verify `X-Wallet-Signature` over the raw body using a KMS-stored secret; check `X-Wallet-Timestamp` within ±5 minutes to block replays
    - _Requirements: 3.5_

  - [x] 9.6 Implement `POST /v1/payments/webhook`
    - Verify signature → conditional persist → look up driver in `WsConnections` → if connected, push `payment.confirmed` envelope via `PostToConnection`; if offline, enqueue in `QueuedMessages` with 24h TTL
    - Reject invalid/missing signature with 401 and no fan-out
    - Idempotent: duplicate `eventId` returns 200 with no side effects
    - Meet p95 ≤ 1500ms webhook → driver-delivery target
    - _Requirements: 3.1, 3.2, 3.5, 3.6, 3.7_

  - [x] 9.7 Implement `POST /v1/payments/audio-failures` logging endpoint
    - Records driver-side audio playback failures (muted device, autoplay blocked) for CloudWatch observability
    - _Requirements: 3.8_

  - [x] 9.8 Write FsCheck property test for PaymentEvent round-trip
    - **Property 2: Round-trip PaymentEvent serialization**
    - **Validates: Requirements 3.10, 3.11**
    - ≥100 iterations; tag `Feature: biyahero-mvp, Property 2: Round-trip PaymentEvent serialization`

  - [x] 9.9 Write xUnit tests for Payment_Service
    - Invalid signature → 401 + no fan-out, duplicate `eventId` is no-op, replay-window timestamp rejected
    - _Requirements: 3.5, 3.7_

  - [x] 9.10 Write integration test for the Anti-123 end-to-end flow
    - Webhook → DynamoDB persist → mocked `PostToConnection` when driver connected; queue + drain on `$connect` when driver offline
    - _Requirements: 3.1, 3.2, 3.6_

- [x] 10. Health and free-tier ops
  - [x] 10.1 Implement `GET /v1/health`
    - Always returns 200 with per-dependency status (Postgres, DynamoDB, WebSocket fan-out reachability)
    - _Requirements: 7.6_

  - [x] 10.2 Implement free-tier usage CloudWatch alarm Lambda
    - Emit operational alert when projected monthly usage of a service exceeds 80% AND actual usage is at or above 50% of the free-tier allowance
    - _Requirements: 7.4, 7.5_

- [x] 11. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Frontend PWA shell and infrastructure
  - [x] 12.1 Configure `next-pwa` and Web App Manifest
    - `manifest.json` with `name=BiyaHero`, `start_url=/`, `display=standalone`, theme color, background color, 192×192 + 512×512 icons
    - Lighthouse PWA installability gate in CI
    - _Requirements: 6.1, 6.6_

  - [x] 12.2 Configure Service Worker cache-first strategy
    - Cache app shell (HTML, CSS, JS, fonts, manifest, icons); cache map tiles and Route data on the read path
    - SW update prompt + reload banner
    - _Requirements: 6.2, 6.3, 6.5, 6.7_

  - [x] 12.3 Configure Tailwind tokens for accessibility and mobile-first
    - 44×44px hit-target utility classes, WCAG 2.1 AA color palette, 320px-min layouts, visible focus indicators
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 12.4 Implement single Axios client + `endpoints.ts` constants
    - JWT bearer interceptor; centralized error transform aligned with the backend error envelope; all paths defined in one constants file per the React/Vite SPA steering
    - _Requirements: 5.3, 5.4, 8.4_

  - [x] 12.5 Implement IndexedDB offline write queue and ordered replay
    - Queue write requests (new Route, Route edit, Demand_Ping cancellation) while offline; replay in submission order on reconnect
    - _Requirements: 6.4_

  - [x] 12.6 Implement online/offline indicator and SW update banner
    - Visible offline indicator driven by `navigator.onLine`; non-blocking banner offering reload when SW update is waiting
    - _Requirements: 6.7, 6.8_

  - [x] 12.7 Implement `next-intl` provider and translation bundles
    - Load `en.json` + `fil.json` versioned bundles at app start; default detection from `navigator.language` (`en` if English, otherwise `fil`); missing-key fallback to English + POST to `/v1/i18n/missing-keys`
    - Initial bundles cover navigation, route plotting, fare calculation, payment dashboard, heatmap controls, login/registration, settings, error messages, offline indicator
    - _Requirements: 10.1, 10.2, 10.6, 10.7, 10.8_

  - [x] 12.8 Implement language-preference store with localStorage mirror and server sync
    - Zustand store; mirrored to localStorage on every change; on login, server-stored preference overrides the local mirror within 1s; UI updates within 100ms without page reload
    - _Requirements: 10.3, 10.4, 10.5_

- [x] 13. Frontend feature slices
  - [x] 13.1 Auth feature slices
    - `register`, `verify-email`, `login`, `refresh`, `logout`, `settings/language-preference`
    - Each is a feature folder with `types.ts`, `schema.ts` (Zod), `useXxx.ts`, `XxxForm.tsx`, `XxxPage.tsx`
    - _Requirements: 5.1, 5.2, 5.3, 5.6, 10.3_

  - [x] 13.2 Route-plot feature slice
    - Map view, plot ≥2 waypoints, submit (status `unverified`), edit-as-revision, accuracy vote
    - Optimistic UI; offline writes queued via task 12.5
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 6.4_

  - [x] 13.3 Fare-calculator feature slice
    - Origin/destination picker, vehicle type select, optional discount category select, results panel showing fare PHP + distance km + matrix version
    - _Requirements: 2.1, 2.5, 2.9_

  - [x] 13.4 Commuter heatmap feature slice
    - WebSocket connect on entry, submit `demand-ping`, cancel via `cancel-demand`
    - _Requirements: 4.1, 4.5_

  - [x] 13.5 Driver heatmap feature slice
    - WebSocket subscribe-heatmap with bbox; render geohash7 demand tiles in real time; never display commuter identity
    - _Requirements: 4.2, 4.3, 4.6_

  - [x] 13.6 Driver payment dashboard feature slice
    - `payment.confirmed` listener; render payer name + amount + timestamp; play TTS Audio_Confirmation in user's language preference (Filipino fallback when English voice missing); high-contrast banner fallback when audio is muted/blocked + POST to `/v1/payments/audio-failures`
    - _Requirements: 3.3, 3.4, 3.8, 10.4_

  - [x] 13.7 Admin feature slice (Super Admin)
    - User list, suspend, promote (with re-entered password 2FA modal before role change)
    - _Requirements: 5.8, 5.9, 5.11_

  - [x] 13.8 Vitest + Testing Library component tests
    - Happy path + at least one error path per feature slice
    - _Requirements: 1.1, 2.1, 3.3, 4.1, 5.1_

  - [x] 13.9 axe-core accessibility audit
    - Run on representative pages at 320px viewport; verify WCAG 2.1 AA contrast, accessible names on every actionable control (including map markers used to submit Demand_Pings), keyboard-only completion of route search, fare calculation, and waiting-for-ride flows
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 13.10 Playwright offline scenario test
    - Cached app shell loads with `network: offline`; queued offline writes replay in submission order on reconnect
    - _Requirements: 6.3, 6.4, 6.5, 6.6_

- [x] 14. AWS infrastructure (CDK)
  - [x] 14.1 Implement `DataStack`
    - RDS PostgreSQL t4g.micro with PostGIS; DynamoDB tables `DemandPings`, `PaymentEvents`, `WsConnections`, `QueuedMessages` with their GSIs and TTL attributes; KMS keys for JWT signing and webhook signing
    - _Requirements: 1.2, 3.1, 3.6, 3.7, 4.1, 4.4, 5.7, 7.1, 7.4, 8.2_

  - [x] 14.2 Implement `ApiStack`
    - API Gateway REST API + WebSocket API + Lambda function per handler (Auth, Routing, Fare, Payment, Heatmap, WS `$connect`/`$disconnect`/route handlers, Aggregator); IAM roles; SES identity for verification email; EventBridge rule for the 5-second aggregator cadence
    - _Requirements: 2.9, 3.2, 4.2, 4.3, 5.1, 7.1, 7.2, 7.3_

  - [x] 14.3 Implement `FrontendStack`
    - CloudFront + S3 origin (or Amplify Hosting) for the PWA build; HTTP→HTTPS redirect; TLS 1.2+ only
    - _Requirements: 6.1, 6.6, 8.4_

  - [x] 14.4 Implement `MonitoringStack`
    - CloudWatch log groups for audit log (≥30-day retention) and access log; alarm wiring for the free-tier-usage Lambda from task 10.2
    - _Requirements: 5.10, 7.5, 8.5_

- [x] 15. Load tests (k6)
  - [x] 15.1 k6 REST mixed workload script
    - 50 concurrent users; assert REST p95 ≤ 400ms
    - _Requirements: 7.2_

  - [x] 15.2 k6 WebSocket fan-out script
    - 200 concurrent connections; assert zero dropped messages
    - _Requirements: 7.3_

  - [x] 15.3 k6 heatmap aggregation script
    - 500k pings/month equivalent rate; assert tile-aggregation p95 ≤ 500ms
    - _Requirements: 4.2, 7.4_

- [x] 16. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery; they cover unit, property, integration, accessibility, offline-scenario, and load tests.
- Each task references specific requirement clauses for traceability; property tests explicitly cite their corresponding property number from the design's Correctness Properties section.
- The four FsCheck property tests (Property 1 round-trip Route, Property 2 round-trip PaymentEvent, Property 3 fare determinism, Property 4 heatmap conservation) live close to the implementation that produces them so failures surface during the same task wave.
- Checkpoint tasks (6, 11, 16) gate progress and exist to surface questions before they compound.
- The plan keeps the OOP layering intact: handlers → domain (`BaseDomain`-derived classes) → repositories → services (the only place where AWS SDK calls appear). REST paths are kebab-case plural with `:custom` for non-CRUD operations, per the REST steering.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.4", "2.5", "2.6", "3.1", "3.2", "3.3", "3.4", "12.1", "12.3"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.7", "12.2", "12.4"] },
    { "id": 3, "tasks": ["4.1", "5.1", "7.1", "7.2", "8.1", "8.4", "9.1", "9.4", "9.5", "12.5", "12.6", "12.7", "12.8"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "5.2", "8.2", "8.3", "9.2", "9.3", "5.8", "7.4", "9.8"] },
    { "id": 5, "tasks": ["4.5", "4.6", "4.7", "4.8", "4.9", "4.10", "4.11", "4.12", "4.13", "5.3", "5.4", "5.5", "5.6", "5.7", "7.3", "8.5", "8.6", "8.7", "8.8", "8.9", "8.10", "9.6", "9.7", "13.1", "13.2", "13.3", "13.4", "13.5", "13.6", "13.7"] },
    { "id": 6, "tasks": ["8.11", "4.14", "5.9", "7.5", "8.12", "8.13", "9.9", "9.10", "13.8", "13.9", "13.10", "10.1", "10.2"] },
    { "id": 7, "tasks": ["14.1", "14.2", "14.3", "14.4"] },
    { "id": 8, "tasks": ["15.1", "15.2", "15.3"] }
  ]
}
```
