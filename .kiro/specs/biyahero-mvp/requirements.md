# Requirements Document

## Introduction

BiyaHero is a Progressive Web App (PWA) that addresses recurring pain points faced by Philippine commuters and Public Utility Vehicle (PUV) drivers — specifically jeepney passengers and operators. The MVP focuses on four high-value problem domains:

1. **Community-Sourced Routing** — a wiki-style map of locally-known commuter routes that users plot, edit, and verify, since many Philippine routes (especially informal jeepney lines) are not represented in mainstream mapping services.
2. **Anti-Scam Fare Calculator** — a deterministic fare estimator that computes the legally correct fare between two points based on geospatial distance and the published Land Transportation Franchising and Regulatory Board (LTFRB) jeepney/PUV fare matrix, protecting commuters from overcharging.
3. **Anti-123 Payment Notifications** — a real-time, server-pushed audio and text confirmation delivered to the driver's dashboard the moment a passenger pays via a digital wallet, so a driver cannot fake a successful payment confirmation (the "123 scam").
4. **Heatmap Dispatcher** — a real-time geolocation system where commuters waiting for a ride appear as glowing demand hotspots on nearby drivers' maps, helping drivers reach passengers faster and reducing commuter wait times.

The system is architected as a strict separation between a thin Next.js + Tailwind CSS PWA frontend (mobile-first, installable, offline-resilient) and a hardcore C# .NET Core Web API backend hosted on AWS. Real-time interactions use AWS API Gateway WebSockets. Hot/ephemeral data (heatmap pings, payment events) lives in DynamoDB; relational data (routes, users, verifications) lives in PostgreSQL on Amazon RDS. The MVP must be deployable within AWS Free Tier limits.

> **MVP scope (confirmed):**
> - **Geographic launch:** Metro Manila is the launch market for marketing and onboarding, but the system supports nationwide community submissions from day 1. Routes and Demand_Pings anywhere within the Philippines must be storable and queryable without geofence rejection at the data model or API layer. Marketing scoping does not constrain the system's accepted coordinate space.
> - **User roles:** four roles — Commuter, Driver, Moderator, Super Admin.
> - **Payment integration:** mocked end-to-end (a sandbox digital-wallet stub, no real money) behind a swappable wallet adapter interface so a real provider can be plugged in post-MVP without changing the WebSocket notification flow.
> - **Authentication:** email + password with email verification, JWT bearer tokens for the REST API, and signed connection tokens for WebSockets. No social login in MVP.
> - **Localization:** bilingual English and Filipino (Tagalog) with a per-user Language_Preference. The default is auto-detected from the browser locale and falls back to Filipino unless the browser indicates English.

## Glossary

- **123_Scam**: A scam where a driver claims to "see" or "hear" the wallet payment confirmation but the payment never actually completed, defrauding the commuter.
- **Audio_Confirmation**: A spoken phrase produced via Text-to-Speech and played on the Driver's device when a Payment_Event arrives.
- **Auth_Service**: The Backend_API subsystem that handles registration, login, email verification, token issuance, role enforcement, and user-management actions performed by Super Admins.
- **AWS_Free_Tier**: The AWS service usage allowance designed for new accounts, used as the cost ceiling for the MVP deployment.
- **Backend_API**: The C# .NET Core Web API exposing REST endpoints to the Frontend.
- **BiyaHero**: The Progressive Web App and backend system described by this document.
- **Commuter**: A registered user whose primary role is finding rides and paying fares.
- **Demand_Ping**: A short-lived geolocation event submitted by a Commuter indicating they are waiting for a ride at a specific coordinate.
- **Driver**: A registered user who operates a PUV and receives passenger demand data and payment confirmations.
- **Fare_Calculator**: The Backend_API subsystem that computes fares from geospatial input using LTFRB rules.
- **Frontend**: The Next.js + Tailwind CSS + next-pwa client application.
- **Heatmap_Service**: The Backend_API subsystem that ingests geolocation pings from Commuters and serves aggregated demand data to Drivers.
- **Heatmap_Tile**: A spatial bucket (e.g., geohash prefix) with an aggregated count of active Demand_Pings, returned to Drivers.
- **Jeepney**: A shared, fixed-route public transit vehicle common in the Philippines.
- **JWT**: JSON Web Token, the bearer token format used for REST API authentication.
- **Language_Preference**: A user-selectable setting (`en` for English or `fil` for Filipino/Tagalog) controlling all UI strings and Audio_Confirmation output language.
- **LTFRB (Land Transportation Franchising and Regulatory Board)**: The Philippine government agency that publishes the official PUV fare matrix.
- **LTFRB_Fare_Matrix**: The published table of base fares, per-kilometer increments, and discount rules (student, senior, PWD) used by the Fare_Calculator.
- **Moderator**: A registered user with elevated permissions who can verify, edit, or remove community-submitted Route revisions.
- **Payment_Event**: A structured message describing a completed digital-wallet payment (amount, currency, payer ID, driver ID, route ID, timestamp).
- **Payment_Service**: The Backend_API subsystem that ingests digital-wallet payment events and forwards them to the WebSocket_Service.
- **PUV (Public Utility Vehicle)**: Any licensed public transport vehicle in the Philippines, including jeepneys, UV Express, and traditional buses.
- **PWA (Progressive Web App)**: A web application that is installable to a device home screen, runs offline via a Service Worker, and is delivered through a Web App Manifest.
- **Route**: A named, ordered sequence of geospatial waypoints describing a PUV path, with metadata (vehicle type, base fare, verification status).
- **Routing_Service**: The Backend_API subsystem that stores, retrieves, and manages community-sourced routes.
- **Service_Worker**: The browser-managed background script that intercepts network requests for offline resilience and caching.
- **Super Admin**: A registered user with platform-wide read, write, and delete permissions, including user management (suspend, role change) and override or removal of any Route, Payment_Event, or Demand_Ping.
- **System**: Any Backend_API subsystem when the requirement applies generically.
- **Waypoint**: A single geographic point (latitude, longitude, optional name) that is part of a Route.
- **Web_App_Manifest**: The JSON file that declares PWA installability metadata (name, icons, display mode, start URL).
- **WebSocket_Service**: The AWS API Gateway WebSocket API and connected backend handlers that push real-time messages to clients.

## Requirements

### Requirement 1: Community-Sourced Routing

**User Story:** As a Commuter, I want to plot, browse, and verify local PUV routes on a community-editable map, so that I can find rides on informal jeepney lines that mainstream mapping apps do not cover.

#### Acceptance Criteria

1. WHEN an authenticated Commuter submits a new Route with a name, vehicle type, ordered list of at least two Waypoints, and a base fare, THE Routing_Service SHALL persist the Route to PostgreSQL and return the created Route with status "unverified".
2. WHEN any client requests Routes within a bounding box defined by southwest and northeast coordinates, THE Routing_Service SHALL return all Routes whose Waypoints intersect that bounding box within 800 milliseconds at the 95th percentile under MVP load.
3. WHEN an authenticated Commuter submits an edit (added, removed, or reordered Waypoints) to an existing Route, THE Routing_Service SHALL store the edit as a pending revision linked to the original Route without overwriting the verified version.
4. WHEN an authenticated Moderator approves a pending revision, THE Routing_Service SHALL replace the active Route definition with the approved revision and SHALL set the Route status to "verified".
5. WHEN an authenticated Commuter marks a verified Route as "still accurate" or "no longer accurate", THE Routing_Service SHALL record the vote with the Commuter ID, Route ID, and timestamp.
6. IF an unauthenticated client attempts to submit, edit, or vote on a Route, THEN THE Routing_Service SHALL reject the request with HTTP 401.
7. IF a submitted Route contains fewer than two Waypoints or any Waypoint outside valid latitude/longitude ranges, THEN THE Routing_Service SHALL reject the request with HTTP 422 and a descriptive error message.
8. THE Routing_Service SHALL accept and persist Routes whose Waypoints fall anywhere within the bounding box of the Republic of the Philippines (approximately latitude 4.5° to 21.5° North, longitude 116° to 127° East) AND SHALL NOT reject Routes solely because they fall outside the Metro Manila MVP launch region.
9. THE Routing_Service SHALL expose a serializer that converts a Route entity to a JSON representation containing route ID, name, vehicle type, ordered Waypoints, base fare, status, and verification vote counts.
10. THE Routing_Service SHALL expose a parser that accepts the JSON representation and reconstructs an equivalent Route entity.
11. FOR ALL valid Route entities, parsing the serialized JSON and re-serializing the parsed Route SHALL produce JSON that is semantically equivalent to the original (round-trip property).

### Requirement 2: Anti-Scam Fare Calculator

**User Story:** As a Commuter, I want to compute the exact legally correct fare between any two points along a Route, so that I can detect when a driver overcharges me.

#### Acceptance Criteria

1. WHEN a client requests a fare estimate with an origin Waypoint, destination Waypoint, vehicle type, and optional discount category (regular, student, senior, PWD), THE Fare_Calculator SHALL return the fare amount in Philippine Peso (PHP), the geospatial distance in kilometers, and the LTFRB_Fare_Matrix version used in the calculation.
2. THE Fare_Calculator SHALL compute geospatial distance using the great-circle (haversine) formula on the WGS84 reference ellipsoid.
3. WHEN the computed distance is less than or equal to the minimum-fare threshold for the vehicle type in the LTFRB_Fare_Matrix, THE Fare_Calculator SHALL return the minimum fare.
4. WHEN the computed distance exceeds the minimum-fare threshold, THE Fare_Calculator SHALL return the minimum fare plus the per-kilometer increment multiplied by the kilometers above the threshold, rounded to the nearest 25 centavos.
5. WHERE a discount category is supplied, THE Fare_Calculator SHALL apply the discount percentage published by LTFRB for that category to the computed fare before rounding.
6. IF the origin or destination contains an invalid coordinate or an unsupported vehicle type, THEN THE Fare_Calculator SHALL reject the request with HTTP 422 and a descriptive error message AND SHALL NOT include any computed fare value in the response, even if a partial computation was performed.
7. IF the request is malformed in ways other than invalid coordinates or unsupported vehicle type (missing required fields, invalid JSON, wrong content type), THEN THE Fare_Calculator SHALL reject the request with HTTP 400.
8. THE Fare_Calculator SHALL load the LTFRB_Fare_Matrix from a versioned configuration source so that fare rules can be updated without redeploying the Backend_API.
9. THE Fare_Calculator SHALL respond within 200 milliseconds at the 95th percentile under MVP load.
10. FOR ALL valid input pairs (origin, destination, vehicle, discount), the Fare_Calculator SHALL return identical results when invoked twice with identical inputs (deterministic property).

### Requirement 3: Anti-123 Real-Time Payment Notifications

**User Story:** As a Driver, I want to hear and see an unforgeable audio and text confirmation on my dashboard the moment a passenger's digital-wallet payment completes, so that I cannot be tricked by the "123 scam" and passengers cannot be falsely accused of non-payment.

#### Acceptance Criteria

1. WHEN a mock digital-wallet provider posts a Payment_Event to the Payment_Service webhook, THE Payment_Service SHALL validate the event signature, persist the event to DynamoDB, and forward a confirmation message to the WebSocket_Service for delivery to the target Driver.
2. WHEN the WebSocket_Service receives a payment confirmation message for a Driver who is currently connected, THE WebSocket_Service SHALL deliver the message to that Driver's connection within 1500 milliseconds of receiving the original webhook at the 95th percentile.
3. WHEN the Driver client receives a payment confirmation message, THE Frontend SHALL display the payer name, amount, and timestamp on the dashboard AND SHALL play an Audio_Confirmation produced by the browser Speech Synthesis API stating the payer name and amount.
4. WHEN the Frontend plays an Audio_Confirmation, THE Frontend SHALL use the Driver's Language_Preference for the TTS voice and phrasing AND SHALL fall back to Filipino phrasing if the requested language voice is unavailable on the device.
5. IF the Payment_Event signature is invalid or missing, THEN THE Payment_Service SHALL reject the webhook with HTTP 401 and SHALL NOT forward any message to the WebSocket_Service.
6. IF the target Driver is not currently connected when a payment confirmation is forwarded, THEN THE WebSocket_Service SHALL queue the message in DynamoDB and deliver the message when the Driver reconnects, for a retention window of at least 24 hours.
7. WHEN the same Payment_Event ID is posted to the Payment_Service more than once, THE Payment_Service SHALL persist the event exactly once and SHALL forward at most one confirmation to the WebSocket_Service (idempotence property).
8. IF the Driver client cannot play audio because audio is muted or autoplay is blocked, THEN THE Frontend SHALL display a high-contrast visual confirmation banner and SHALL log the audio failure to the Backend_API.
9. THE Payment_Service SHALL route all outbound digital-wallet calls through a swappable wallet adapter interface so that the mocked provider can be replaced with a real provider post-MVP without changing the Payment_Event schema or the WebSocket notification flow.
10. THE Payment_Service SHALL expose a serializer that converts a Payment_Event to a JSON representation and a parser that reconstructs a Payment_Event from the JSON representation.
11. FOR ALL valid Payment_Event entities, parsing the serialized JSON and re-serializing the parsed event SHALL produce JSON that is semantically equivalent to the original (round-trip property).

### Requirement 4: Heatmap Dispatcher

**User Story:** As a Driver, I want to see a real-time heatmap of nearby commuters waiting for rides, so that I can drive toward demand and earn more.

**User Story:** As a Commuter, I want my "waiting for a ride" status to be visible to nearby drivers without exposing my exact identity, so that I get picked up faster while preserving my privacy.

#### Acceptance Criteria

1. WHEN an authenticated Commuter submits a Demand_Ping containing latitude, longitude, vehicle type, and timestamp via WebSocket, THE Heatmap_Service SHALL persist the ping to DynamoDB with a time-to-live (TTL) of 5 minutes.
2. WHEN any client (authenticated or unauthenticated) requests Heatmap_Tiles for a bounding box via WebSocket or REST, THE Heatmap_Service SHALL return aggregated demand counts grouped by geohash precision 7 within 500 milliseconds at the 95th percentile.
3. WHILE a Driver maintains an open WebSocket connection with an active heatmap subscription, THE Heatmap_Service SHALL push updated Heatmap_Tile aggregates at intervals not greater than 5 seconds whenever demand counts in the subscribed bounding box change.
4. WHEN a Demand_Ping reaches its TTL, THE Heatmap_Service SHALL automatically remove the ping from DynamoDB AND SHALL exclude the ping from subsequent Heatmap_Tile aggregations.
5. WHEN a Commuter submits a "ride accepted" or "cancel waiting" message via WebSocket, THE Heatmap_Service SHALL remove that Commuter's active Demand_Ping immediately.
6. THE Heatmap_Service SHALL never include personally identifying information (Commuter ID, name, email, or device ID) in Heatmap_Tile responses sent to Drivers.
7. IF an unauthenticated client attempts to submit a Demand_Ping, THEN THE WebSocket_Service SHALL terminate the connection with a 4001 close code AND SHALL ensure the underlying connection is fully closed even if the close-code frame fails to deliver to the client. Subscribing to read-only Heatmap_Tile streams SHALL be permitted for unauthenticated clients.
8. IF a Demand_Ping contains an invalid coordinate or an unsupported vehicle type, THEN THE Heatmap_Service SHALL reject the ping with an error message and SHALL NOT persist the ping.
9. THE Heatmap_Service SHALL accept Demand_Pings whose coordinates fall anywhere within the bounding box of the Republic of the Philippines (approximately latitude 4.5° to 21.5° North, longitude 116° to 127° East) AND SHALL NOT reject Demand_Pings solely because they fall outside the Metro Manila MVP launch region.
10. FOR ALL Demand_Pings submitted within an arbitrary 60-second window, the count of Heatmap_Tile aggregations SHALL equal the count of valid pings minus pings removed by TTL or cancellation (conservation property).

### Requirement 5: Authentication and Authorization

**User Story:** As a user, I want a secure account so that my routes, pings, and payments are tied to my identity and protected from impersonation.

#### Acceptance Criteria

1. WHEN a user submits a registration request with an email, password, role (Commuter or Driver), and display name, THE Auth_Service SHALL create the account in PostgreSQL with status "pending_verification" and SHALL send a verification email containing a single-use token valid for 24 hours.
2. WHEN a user submits a valid verification token, THE Auth_Service SHALL set the account status to "active" AND SHALL invalidate the token.
3. WHEN an active user submits a login request with correct credentials, THE Auth_Service SHALL return a signed JWT with an expiration of 24 hours and a refresh token with an expiration of 30 days.
4. WHEN a client opens a WebSocket connection, THE WebSocket_Service SHALL require a valid JWT in the connection handshake AND SHALL reject the connection with a 4001 close code if the JWT is missing, expired, or invalid.
5. IF a registration request supplies an email that already has an active account, THEN THE Auth_Service SHALL reject the request with HTTP 409 and SHALL NOT reveal whether the existing account is verified.
6. IF a login request supplies incorrect credentials, THEN THE Auth_Service SHALL reject the request with HTTP 401 and a generic error message that does not distinguish between "unknown email" and "wrong password".
7. THE Auth_Service SHALL store passwords using a memory-hard hashing algorithm (Argon2id or bcrypt with cost factor at least 12).
8. WHERE a user has the Moderator role, THE Backend_API SHALL grant access to route-revision approval endpoints; otherwise THE Backend_API SHALL reject access with HTTP 403.
9. WHERE a user has the Super Admin role, THE Backend_API SHALL grant access to all platform resources, including user-management endpoints (suspend, role change), and SHALL grant override or delete permissions on any Route, Payment_Event, or Demand_Ping regardless of original ownership.
10. WHEN a Super Admin performs any write or delete action through the Backend_API, THE Auth_Service SHALL append an immutable audit log entry to a CloudWatch log group containing the Super Admin user ID, the target resource identifier, the action performed, and a UTC timestamp.
11. IF a Super Admin attempts to escalate another user to the Super Admin role, THEN THE Auth_Service SHALL require a second-factor confirmation by re-entered password before applying the role change.

### Requirement 6: PWA Installability and Offline Resilience

**User Story:** As a commuter on flaky Philippine mobile data, I want to install BiyaHero to my home screen and use core features even when my connection is unreliable, so that the app works during my actual commute.

#### Acceptance Criteria

1. THE Frontend SHALL serve a Web_App_Manifest declaring the app name "BiyaHero", a `start_url` of `/`, a `display` of `standalone`, a theme color, a background color, and icons at 192×192 and 512×512 pixels.
2. THE Frontend SHALL register a Service_Worker on first load that caches the application shell (HTML, CSS, JS, fonts, manifest, and icons) using a cache-first strategy.
3. WHEN the Frontend is loaded while the device is offline, THE Service_Worker SHALL serve the cached application shell AND THE Frontend SHALL display the most recently cached Routes for the active map viewport.
4. WHEN the Frontend submits a write request (new Route, Route edit, Demand_Ping cancellation) while offline, THE Frontend SHALL queue the request in IndexedDB AND SHALL replay queued requests in submission order once connectivity is restored.
5. WHILE the device is online but bandwidth is below 200 kilobits per second sustained, THE Frontend SHALL continue to serve cached map tiles and Route data from the Service_Worker cache instead of refetching, AND WHILE the device is offline, THE Frontend SHALL serve only previously cached map tiles and Route data and SHALL NOT issue network requests for refresh.
6. THE Frontend SHALL pass the Lighthouse PWA installability audit with no failing checks on a Chromium-based browser.
7. IF a Service_Worker update is available, THEN THE Frontend SHALL prompt the user with a non-blocking banner offering to reload the app to apply the update.
8. THE Frontend SHALL display a clearly visible offline indicator whenever the device reports `navigator.onLine === false`.

### Requirement 7: Real-Time Performance and AWS Free Tier Deployability

**User Story:** As the project owner, I want the MVP to run within AWS Free Tier limits and meet basic real-time targets, so that the project is sustainable to operate during early validation.

#### Acceptance Criteria

1. THE Backend_API SHALL be deployable to AWS Elastic Beanstalk or AWS App Runner using a single environment configuration that targets a free-tier-eligible instance class.
2. THE Backend_API SHALL respond to REST requests within 400 milliseconds at the 95th percentile under a sustained load of 50 concurrent users.
3. THE WebSocket_Service SHALL sustain at least 200 concurrent connections without dropped messages under MVP test load.
4. THE System SHALL keep monthly AWS spend under the AWS_Free_Tier allowance for new accounts as long as monthly active users remain below 1000 and monthly Demand_Pings remain below 500,000.
5. WHEN AWS_Free_Tier usage of any individual service is projected to exceed 80 percent for the current month AND the actual measured usage of that service is at or above 50 percent of the AWS_Free_Tier allowance, THE System SHALL emit an operational alert to a configured notification channel.
6. THE Backend_API SHALL expose a `/health` endpoint that returns HTTP 200 with a JSON body reporting the status of database and WebSocket dependencies, regardless of whether individual dependencies report as healthy or unhealthy, used by Elastic Beanstalk health checks.

### Requirement 8: Data Privacy and Retention

**User Story:** As a Commuter, I want the app to retain only the minimum data necessary to provide the service, so that my movement and payment history are not exploited.

#### Acceptance Criteria

1. THE Heatmap_Service SHALL retain Demand_Ping records for no longer than 5 minutes after submission via DynamoDB TTL, after which records SHALL be either deleted or anonymized by removing Commuter ID and device ID.
2. THE Payment_Service SHALL retain Payment_Event records for no longer than 90 days, after which records SHALL be either deleted or anonymized by removing payer ID and amount.
3. WHEN a user submits a deletion request via the account settings endpoint, THE Auth_Service SHALL delete or anonymize the user's Routes contributions, Demand_Pings, and Payment_Event payer references within 30 days.
4. THE Backend_API SHALL transmit all REST and WebSocket traffic over TLS 1.2 or higher.
5. THE Backend_API SHALL log access to Payment_Event and Auth_Service endpoints with caller identity, endpoint, timestamp, and outcome to a CloudWatch log group with at least 30-day retention.

### Requirement 9: Accessibility and Mobile-First UX

**User Story:** As a commuter using BiyaHero on a small phone in bright sunlight, I want a high-contrast, large-target interface, so that I can use the app one-handed at a jeepney stop.

#### Acceptance Criteria

1. THE Frontend SHALL ensure all interactive controls render with a hit target of at least 44 by 44 CSS pixels.
2. THE Frontend SHALL meet WCAG 2.1 Level AA contrast ratios for all text and meaningful non-text UI elements as verified by automated audit (Lighthouse or axe).
3. THE Frontend SHALL render and remain usable on viewports as narrow as 320 CSS pixels.
4. WHEN a user navigates the Frontend using only a keyboard, THE Frontend SHALL provide visible focus indicators on every interactive control AND SHALL allow completion of route search, fare calculation, and waiting-for-ride flows without a pointing device.
5. THE Frontend SHALL provide accessible names (via `aria-label` or visible text) for every actionable control, including map markers used to submit Demand_Pings.

### Requirement 10: Localization and Language Preference

**User Story:** As a Filipino jeepney driver who is more comfortable in Tagalog than English, I want the app and its audio confirmations to speak my language by default and let me change it any time, so that I can use BiyaHero confidently during my workday.

#### Acceptance Criteria

1. THE Frontend SHALL support a Language_Preference value of either `en` (English) or `fil` (Filipino/Tagalog) for all visible UI strings.
2. WHEN a user opens the Frontend for the first time on a device with no stored Language_Preference, THE Frontend SHALL set the initial Language_Preference based on the value of `navigator.language`: `en` if the browser indicates English, otherwise `fil`.
3. WHEN an authenticated user changes their Language_Preference from the settings screen, THE Auth_Service SHALL persist the new preference to PostgreSQL on the User record AND THE Frontend SHALL update visible UI strings to the new language within 100 milliseconds without a page reload.
4. WHEN an authenticated user logs in on a new device, THE Frontend SHALL apply the user's server-stored Language_Preference within 1 second of authentication completing.
5. THE Frontend SHALL mirror the active Language_Preference in localStorage so that the most recent preference applies on subsequent loads even when the device is offline.
6. IF a translated string is missing for the active Language_Preference, THEN THE Frontend SHALL fall back to the English string AND SHALL log the missing translation key to the Backend_API for translation backfill.
7. THE Frontend SHALL ship initial translations covering navigation, route plotting, fare calculation, payment dashboard, heatmap controls, login and registration, settings, error messages, and offline indicator strings.
8. THE Frontend SHALL store all UI strings in versioned translation bundles loaded at application start so that translations can be updated without redeploying application code.
