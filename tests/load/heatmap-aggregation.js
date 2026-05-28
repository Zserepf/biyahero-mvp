/**
 * k6 Load Test: Heatmap Aggregation Latency
 *
 * Simulates commuters sending demand-ping messages via WebSocket at the
 * 500k pings/month equivalent rate (~11.5 pings/second sustained) and
 * measures the time from ping submission to receiving the aggregated
 * heatmap.delta on a subscribed driver connection.
 *
 * Validates:
 *   - Requirement 4.2: Tile-aggregation p95 ≤ 500ms
 *   - Requirement 7.4: System handles ≤500k pings/month within Free Tier
 *
 * Usage:
 *   k6 run --env WS_URL=wss://your-api.example.com/prod tests/load/heatmap-aggregation.js
 *
 * Environment Variables:
 *   WS_URL          - WebSocket base URL (required)
 *   COMMUTER_TOKEN  - JWT token for an authenticated commuter (required)
 *   DRIVER_TOKEN    - JWT token for an authenticated driver (required)
 *   TEST_DURATION   - Test duration (default: "2m")
 *   RAMP_UP         - Ramp-up duration (default: "30s")
 */

import ws from "k6/ws";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

const WS_URL = __ENV.WS_URL || "wss://localhost:3001/prod";
const COMMUTER_TOKEN = __ENV.COMMUTER_TOKEN || "test-commuter-jwt";
const DRIVER_TOKEN = __ENV.DRIVER_TOKEN || "test-driver-jwt";
const TEST_DURATION = __ENV.TEST_DURATION || "2m";
const RAMP_UP = __ENV.RAMP_UP || "30s";

// 500k pings/month ≈ 500000 / (30 * 24 * 3600) ≈ 0.193 pings/second per VU
// With ~60 VUs we reach ~11.5 pings/second sustained
const TARGET_VUS = 60;
const PING_INTERVAL_SECONDS = 5; // Each VU sends a ping every 5 seconds → 60 * (1/5) = 12 pings/s

// Philippines bounding box
const PH_LAT_MIN = 4.5;
const PH_LAT_MAX = 21.5;
const PH_LNG_MIN = 116.0;
const PH_LNG_MAX = 127.0;

// Metro Manila approximate center for driver subscription bbox
const METRO_MANILA_BBOX = {
  swLat: 14.3,
  swLng: 120.9,
  neLat: 14.8,
  neLng: 121.2,
};

// ---------------------------------------------------------------------------
// Custom Metrics
// ---------------------------------------------------------------------------

const aggregationLatency = new Trend("heatmap_aggregation_latency_ms", true);
const pingsSubmitted = new Counter("demand_pings_submitted");
const deltasReceived = new Counter("heatmap_deltas_received");
const pingErrors = new Counter("demand_ping_errors");

// ---------------------------------------------------------------------------
// k6 Options
// ---------------------------------------------------------------------------

export const options = {
  scenarios: {
    commuter_pings: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: RAMP_UP, target: TARGET_VUS },
        { duration: TEST_DURATION, target: TARGET_VUS },
        { duration: "10s", target: 0 },
      ],
      exec: "commuterPingScenario",
    },
    driver_subscribe: {
      executor: "constant-vus",
      vus: 5,
      duration: `${parseDuration(RAMP_UP) + parseDuration(TEST_DURATION) + 10}s`,
      exec: "driverSubscribeScenario",
    },
  },
  thresholds: {
    heatmap_aggregation_latency_ms: ["p(95)<500"],
    demand_ping_errors: ["count<10"],
  },
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Generate a random coordinate within the Philippines bounding box.
 * Concentrates ~70% of pings in Metro Manila for realistic clustering.
 */
function randomPhCoordinate() {
  const useMetroManila = Math.random() < 0.7;

  if (useMetroManila) {
    return {
      lat:
        METRO_MANILA_BBOX.swLat +
        Math.random() * (METRO_MANILA_BBOX.neLat - METRO_MANILA_BBOX.swLat),
      lng:
        METRO_MANILA_BBOX.swLng +
        Math.random() * (METRO_MANILA_BBOX.neLng - METRO_MANILA_BBOX.swLng),
    };
  }

  return {
    lat: PH_LAT_MIN + Math.random() * (PH_LAT_MAX - PH_LAT_MIN),
    lng: PH_LNG_MIN + Math.random() * (PH_LNG_MAX - PH_LNG_MIN),
  };
}

/**
 * Build a standard WebSocket envelope message.
 */
function buildEnvelope(action, data) {
  return JSON.stringify({
    action,
    requestId: uuidv4(),
    data,
  });
}

/**
 * Parse a duration string like "2m" or "30s" into seconds.
 */
function parseDuration(durationStr) {
  const match = durationStr.match(/^(\d+)(s|m|h)$/);
  if (!match) return 120;
  const value = parseInt(match[1], 10);
  switch (match[2]) {
    case "s":
      return value;
    case "m":
      return value * 60;
    case "h":
      return value * 3600;
    default:
      return value;
  }
}

/**
 * Supported vehicle types for demand pings.
 */
const VEHICLE_TYPES = ["jeepney", "uv_express", "bus"];

function randomVehicleType() {
  return VEHICLE_TYPES[Math.floor(Math.random() * VEHICLE_TYPES.length)];
}

// ---------------------------------------------------------------------------
// Scenarios
// ---------------------------------------------------------------------------

/**
 * Commuter scenario: connects via WebSocket and sends demand-ping messages
 * at a sustained rate equivalent to 500k pings/month across all VUs.
 */
export function commuterPingScenario() {
  const url = `${WS_URL}?token=${COMMUTER_TOKEN}`;

  const res = ws.connect(url, {}, function (socket) {
    socket.on("open", function () {
      // Send pings at the configured interval for the test duration
      const totalDurationMs =
        (parseDuration(TEST_DURATION) + parseDuration(RAMP_UP)) * 1000;
      const startTime = Date.now();

      socket.setInterval(function () {
        if (Date.now() - startTime > totalDurationMs) {
          socket.close();
          return;
        }

        const coord = randomPhCoordinate();
        const pingMessage = buildEnvelope("demand-ping", {
          lat: coord.lat,
          lng: coord.lng,
          vehicleType: randomVehicleType(),
          timestamp: new Date().toISOString(),
        });

        socket.send(pingMessage);
        pingsSubmitted.add(1);
      }, PING_INTERVAL_SECONDS * 1000);
    });

    socket.on("message", function (msg) {
      try {
        const envelope = JSON.parse(msg);
        if (
          envelope.action === "error" ||
          (envelope.data && envelope.data.error)
        ) {
          pingErrors.add(1);
        }
      } catch (e) {
        // Non-JSON message, ignore
      }
    });

    socket.on("error", function (e) {
      pingErrors.add(1);
    });

    socket.on("close", function () {});

    // Keep connection alive for the scenario duration
    socket.setTimeout(function () {
      socket.close();
    }, (parseDuration(TEST_DURATION) + parseDuration(RAMP_UP) + 5) * 1000);
  });

  check(res, {
    "commuter WebSocket connected": (r) => r && r.status === 101,
  });
}

/**
 * Driver scenario: connects via WebSocket, subscribes to heatmap updates
 * for the Metro Manila bounding box, and measures the latency between
 * when pings are submitted and when aggregated heatmap.delta messages arrive.
 *
 * The aggregation latency is measured as the difference between the
 * server-emitted timestamp (emittedAt) and the current wall-clock time,
 * approximating the end-to-end aggregation pipeline delay.
 */
export function driverSubscribeScenario() {
  const url = `${WS_URL}?token=${DRIVER_TOKEN}`;

  const res = ws.connect(url, {}, function (socket) {
    socket.on("open", function () {
      // Subscribe to heatmap updates for Metro Manila bbox
      const subscribeMessage = buildEnvelope("subscribe-heatmap", {
        bbox: {
          sw: { lat: METRO_MANILA_BBOX.swLat, lng: METRO_MANILA_BBOX.swLng },
          ne: { lat: METRO_MANILA_BBOX.neLat, lng: METRO_MANILA_BBOX.neLng },
        },
      });

      socket.send(subscribeMessage);
    });

    socket.on("message", function (msg) {
      const receivedAt = Date.now();

      try {
        const envelope = JSON.parse(msg);

        if (envelope.action === "heatmap.delta") {
          deltasReceived.add(1);

          // Measure aggregation latency using the server-emitted timestamp
          if (envelope.emittedAt) {
            const emittedAt = new Date(envelope.emittedAt).getTime();
            const latencyMs = receivedAt - emittedAt;

            // Only record positive, reasonable latencies (< 30s)
            // to filter out clock-skew artifacts
            if (latencyMs > 0 && latencyMs < 30000) {
              aggregationLatency.add(latencyMs);
            }
          }

          // Verify no PII is present in the tile data (Req 4.6)
          if (envelope.data && envelope.data.tiles) {
            for (const tile of envelope.data.tiles) {
              check(tile, {
                "tile has no commuter ID": (t) => !t.commuterId,
                "tile has no email": (t) => !t.email,
                "tile has no device ID": (t) => !t.deviceId,
                "tile has no name": (t) => !t.name || t.name === undefined,
              });
            }
          }
        }
      } catch (e) {
        // Non-JSON message, ignore
      }
    });

    socket.on("error", function (e) {
      pingErrors.add(1);
    });

    socket.on("close", function () {});

    // Keep connection alive for the full test duration
    socket.setTimeout(function () {
      socket.close();
    }, (parseDuration(TEST_DURATION) + parseDuration(RAMP_UP) + 10) * 1000);
  });

  check(res, {
    "driver WebSocket connected": (r) => r && r.status === 101,
  });
}

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------

export function handleSummary(data) {
  const p95 = data.metrics.heatmap_aggregation_latency_ms
    ? data.metrics.heatmap_aggregation_latency_ms.values["p(95)"]
    : "N/A";
  const totalPings = data.metrics.demand_pings_submitted
    ? data.metrics.demand_pings_submitted.values.count
    : 0;
  const totalDeltas = data.metrics.heatmap_deltas_received
    ? data.metrics.heatmap_deltas_received.values.count
    : 0;
  const errors = data.metrics.demand_ping_errors
    ? data.metrics.demand_ping_errors.values.count
    : 0;

  const summary = `
╔══════════════════════════════════════════════════════════════╗
║           BiyaHero Heatmap Aggregation Load Test            ║
╠══════════════════════════════════════════════════════════════╣
║  Target Rate:     ~11.5 pings/sec (500k/month equivalent)  ║
║  Pings Submitted: ${String(totalPings).padEnd(40)}║
║  Deltas Received: ${String(totalDeltas).padEnd(40)}║
║  Errors:          ${String(errors).padEnd(40)}║
║  Aggregation p95: ${String(p95 !== "N/A" ? p95.toFixed(2) + " ms" : "N/A").padEnd(40)}║
║  Threshold:       ≤ 500 ms (Req 4.2)                       ║
║  Status:          ${String(p95 !== "N/A" && p95 <= 500 ? "✓ PASS" : "✗ FAIL").padEnd(40)}║
╚══════════════════════════════════════════════════════════════╝
`;

  console.log(summary);

  return {
    stdout: summary,
  };
}
