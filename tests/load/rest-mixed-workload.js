/**
 * k6 REST Mixed Workload Load Test — BiyaHero MVP
 *
 * Simulates 50 concurrent users exercising a mix of REST API endpoints:
 * - Auth (login, me)
 * - Routes (GET list, GET by id)
 * - Fare calculation (POST /v1/fare/:calculate)
 * - Heatmap tiles (GET /v1/heatmap/tiles)
 * - Health check (GET /v1/health)
 *
 * Validates: Requirement 7.2 — REST p95 ≤ 400ms under 50 concurrent users.
 *
 * Usage:
 *   k6 run tests/load/rest-mixed-workload.js
 *
 * Environment variables:
 *   BASE_URL       — API base URL (default: http://localhost:5000)
 *   TEST_EMAIL     — Pre-registered test user email (default: loadtest@biyahero.test)
 *   TEST_PASSWORD  — Test user password (default: LoadTest123!)
 *   ROUTE_ID       — A known route ID for GET /v1/routes/{id} (optional, skipped if not set)
 */

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const TEST_EMAIL = __ENV.TEST_EMAIL || "loadtest@biyahero.test";
const TEST_PASSWORD = __ENV.TEST_PASSWORD || "LoadTest123!";
const ROUTE_ID = __ENV.ROUTE_ID || "";

// Custom metrics
const failRate = new Rate("failed_requests");
const loginDuration = new Trend("login_duration", true);
const meDuration = new Trend("me_duration", true);
const routeListDuration = new Trend("route_list_duration", true);
const routeGetDuration = new Trend("route_get_duration", true);
const fareCalcDuration = new Trend("fare_calc_duration", true);
const heatmapDuration = new Trend("heatmap_duration", true);
const healthDuration = new Trend("health_duration", true);

// ---------------------------------------------------------------------------
// k6 Options
// ---------------------------------------------------------------------------

export const options = {
  scenarios: {
    mixed_workload: {
      executor: "constant-vus",
      vus: 50,
      duration: "2m",
    },
  },
  thresholds: {
    // Requirement 7.2: REST p95 ≤ 400ms
    http_req_duration: ["p(95)<400"],
    // Per-endpoint thresholds for observability
    login_duration: ["p(95)<400"],
    me_duration: ["p(95)<400"],
    route_list_duration: ["p(95)<400"],
    route_get_duration: ["p(95)<400"],
    fare_calc_duration: ["p(95)<400"],
    heatmap_duration: ["p(95)<400"],
    health_duration: ["p(95)<400"],
    // Failure rate should stay below 5%
    failed_requests: ["rate<0.05"],
  },
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function jsonHeaders(token) {
  const headers = { "Content-Type": "application/json" };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }
  return headers;
}

// ---------------------------------------------------------------------------
// Setup — authenticate once and share the token across VUs
// ---------------------------------------------------------------------------

export function setup() {
  const loginPayload = JSON.stringify({
    email: TEST_EMAIL,
    password: TEST_PASSWORD,
  });

  const loginRes = http.post(`${BASE_URL}/v1/auth/sessions`, loginPayload, {
    headers: { "Content-Type": "application/json" },
  });

  let token = "";
  if (loginRes.status === 200) {
    try {
      const body = JSON.parse(loginRes.body);
      token = body.accessToken || "";
    } catch (_) {
      // If login fails, tests will run without auth (some endpoints allow anonymous)
    }
  }

  return { token };
}

// ---------------------------------------------------------------------------
// Main VU function — weighted random endpoint selection
// ---------------------------------------------------------------------------

export default function (data) {
  const token = data.token;

  // Weighted distribution simulating realistic traffic mix:
  //   Health:     5%
  //   Login:     10%
  //   Me:        15%
  //   Routes:    25% (list)
  //   Route ID:  10%
  //   Fare:      20%
  //   Heatmap:   15%
  const roll = Math.random() * 100;

  if (roll < 5) {
    healthCheck();
  } else if (roll < 15) {
    login();
  } else if (roll < 30) {
    getMe(token);
  } else if (roll < 55) {
    listRoutes();
  } else if (roll < 65) {
    getRouteById();
  } else if (roll < 85) {
    calculateFare();
  } else {
    getHeatmapTiles();
  }

  // Brief pause between requests to simulate realistic user think time
  sleep(Math.random() * 1 + 0.5); // 0.5–1.5s
}

// ---------------------------------------------------------------------------
// Endpoint functions
// ---------------------------------------------------------------------------

function healthCheck() {
  const res = http.get(`${BASE_URL}/v1/health`);
  healthDuration.add(res.timings.duration);
  const passed = check(res, {
    "health: status 200": (r) => r.status === 200,
  });
  failRate.add(!passed);
}

function login() {
  const payload = JSON.stringify({
    email: TEST_EMAIL,
    password: TEST_PASSWORD,
  });

  const res = http.post(`${BASE_URL}/v1/auth/sessions`, payload, {
    headers: { "Content-Type": "application/json" },
  });
  loginDuration.add(res.timings.duration);
  const passed = check(res, {
    "login: status 200 or 401": (r) => r.status === 200 || r.status === 401,
  });
  failRate.add(!passed);
}

function getMe(token) {
  if (!token) {
    // Skip if no token available
    return;
  }
  const res = http.get(`${BASE_URL}/v1/auth/me`, {
    headers: jsonHeaders(token),
  });
  meDuration.add(res.timings.duration);
  const passed = check(res, {
    "me: status 200": (r) => r.status === 200,
  });
  failRate.add(!passed);
}

function listRoutes() {
  // Query routes within Metro Manila bounding box
  const params = "?minLat=14.35&minLng=120.90&maxLat=14.75&maxLng=121.15";
  const res = http.get(`${BASE_URL}/v1/routes${params}`);
  routeListDuration.add(res.timings.duration);
  const passed = check(res, {
    "routes list: status 200": (r) => r.status === 200,
  });
  failRate.add(!passed);
}

function getRouteById() {
  if (!ROUTE_ID) {
    // If no route ID configured, fall back to listing routes
    listRoutes();
    return;
  }
  const res = http.get(`${BASE_URL}/v1/routes/${ROUTE_ID}`);
  routeGetDuration.add(res.timings.duration);
  const passed = check(res, {
    "route get: status 200 or 404": (r) =>
      r.status === 200 || r.status === 404,
  });
  failRate.add(!passed);
}

function calculateFare() {
  // Sample fare calculation: ~5km jeepney ride in Manila
  const payload = JSON.stringify({
    originLat: 14.5995,
    originLng: 120.9842,
    destinationLat: 14.5547,
    destinationLng: 121.0244,
    vehicleType: "jeepney",
    discountCategory: "regular",
  });

  const res = http.post(`${BASE_URL}/v1/fare/:calculate`, payload, {
    headers: { "Content-Type": "application/json" },
  });
  fareCalcDuration.add(res.timings.duration);
  const passed = check(res, {
    "fare: status 200": (r) => r.status === 200,
    "fare: has amount": (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.amountPhp !== undefined;
      } catch (_) {
        return false;
      }
    },
  });
  failRate.add(!passed);
}

function getHeatmapTiles() {
  // Query heatmap tiles for Metro Manila area
  const params = "?minLat=14.45&minLng=120.95&maxLat=14.65&maxLng=121.10";
  const res = http.get(`${BASE_URL}/v1/heatmap/tiles${params}`);
  heatmapDuration.add(res.timings.duration);
  const passed = check(res, {
    "heatmap: status 200": (r) => r.status === 200,
  });
  failRate.add(!passed);
}
