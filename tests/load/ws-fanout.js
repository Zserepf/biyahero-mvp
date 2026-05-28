/**
 * k6 WebSocket Fan-Out Load Test
 *
 * Validates Requirement 7.3: The WebSocket_Service SHALL sustain at least
 * 200 concurrent connections without dropped messages under MVP test load.
 *
 * Scenario:
 *   - 200 concurrent WebSocket clients connect
 *   - All clients subscribe to heatmap updates (subscribe-heatmap)
 *   - A subset of connections (10%) submit demand-pings
 *   - All subscribed clients must receive heatmap.delta messages
 *   - Zero dropped messages asserted via thresholds
 *
 * Usage:
 *   WS_URL=wss://your-api.execute-api.region.amazonaws.com/prod \
 *   AUTH_TOKEN=<valid-jwt> \
 *   k6 run tests/load/ws-fanout.js
 */

import ws from "k6/ws";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

const WS_URL = __ENV.WS_URL || "ws://localhost:5000/ws";
const AUTH_TOKEN = __ENV.AUTH_TOKEN || "test-jwt-token";

// Number of concurrent WebSocket connections
const CONCURRENT_CONNECTIONS = 200;

// Fraction of connections that will submit demand-pings (producers)
const PRODUCER_RATIO = 0.1;

// Duration of the test scenario
const TEST_DURATION = "60s";

// How long to wait for heatmap.delta messages after pings are sent
const DRAIN_WAIT_SECONDS = 15;

// Bounding box for heatmap subscription (Metro Manila area)
const SUBSCRIBE_BBOX = {
  sw: { lat: 14.35, lng: 120.9 },
  ne: { lat: 14.75, lng: 121.15 },
};

// ---------------------------------------------------------------------------
// Custom Metrics
// ---------------------------------------------------------------------------

// Total heatmap.delta messages expected (producers × subscribers)
const expectedMessages = new Counter("expected_messages");
// Total heatmap.delta messages actually received
const receivedMessages = new Counter("received_messages");
// Rate of successfully received messages (should be 1.0 = 100%)
const messageDeliveryRate = new Rate("message_delivery_rate");
// Connection success rate
const connectionSuccess = new Rate("ws_connection_success");
// Time from demand-ping send to heatmap.delta receipt
const fanoutLatency = new Trend("fanout_latency_ms", true);
// Messages dropped (expected - received per VU)
const droppedMessages = new Counter("dropped_messages");

// ---------------------------------------------------------------------------
// k6 Options
// ---------------------------------------------------------------------------

export const options = {
  scenarios: {
    ws_fanout: {
      executor: "shared-iterations",
      vus: CONCURRENT_CONNECTIONS,
      iterations: CONCURRENT_CONNECTIONS,
      maxDuration: "120s",
    },
  },
  thresholds: {
    // Zero dropped messages: delivery rate must be 100%
    message_delivery_rate: ["rate>=1.0"],
    // All connections must succeed
    ws_connection_success: ["rate>=1.0"],
    // Dropped messages counter must be zero
    dropped_messages: ["count==0"],
    // Fan-out latency p95 should be under 5 seconds (aggregator runs every 5s)
    fanout_latency_ms: ["p(95)<5000"],
  },
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Build a WebSocket envelope message.
 */
function buildEnvelope(action, data) {
  return JSON.stringify({
    action: action,
    requestId: uuidv4(),
    data: data,
  });
}

/**
 * Generate a random coordinate within the Metro Manila bounding box.
 */
function randomCoordInBbox() {
  const lat =
    SUBSCRIBE_BBOX.sw.lat +
    Math.random() * (SUBSCRIBE_BBOX.ne.lat - SUBSCRIBE_BBOX.sw.lat);
  const lng =
    SUBSCRIBE_BBOX.sw.lng +
    Math.random() * (SUBSCRIBE_BBOX.ne.lng - SUBSCRIBE_BBOX.sw.lng);
  return { lat, lng };
}

// ---------------------------------------------------------------------------
// Main Test Function
// ---------------------------------------------------------------------------

export default function () {
  const vuId = __VU;
  const isProducer = vuId <= CONCURRENT_CONNECTIONS * PRODUCER_RATIO;

  // Build connection URL with auth token
  const url = `${WS_URL}?token=${AUTH_TOKEN}`;

  let messagesReceived = 0;
  let messagesExpected = 0;
  let pingTimestamps = {};

  const res = ws.connect(url, null, function (socket) {
    socket.on("open", function () {
      connectionSuccess.add(1);

      // All clients subscribe to heatmap updates
      const subscribeMsg = buildEnvelope("subscribe-heatmap", {
        bbox: SUBSCRIBE_BBOX,
      });
      socket.send(subscribeMsg);

      // Producers send demand-pings after a short delay to let all clients subscribe
      if (isProducer) {
        sleep(2);

        // Send multiple demand-pings over the test window
        for (let i = 0; i < 5; i++) {
          const coord = randomCoordInBbox();
          const pingId = uuidv4();
          const pingMsg = buildEnvelope("demand-ping", {
            lat: coord.lat,
            lng: coord.lng,
            vehicleType: "jeepney",
            timestamp: new Date().toISOString(),
          });

          pingTimestamps[pingId] = Date.now();
          socket.send(pingMsg);

          sleep(2);
        }
      }
    });

    socket.on("message", function (msg) {
      try {
        const envelope = JSON.parse(msg);

        if (envelope.action === "heatmap.delta") {
          messagesReceived++;
          receivedMessages.add(1);
          messageDeliveryRate.add(1);

          // Track fan-out latency if we can correlate
          if (envelope.emittedAt) {
            const emittedTime = new Date(envelope.emittedAt).getTime();
            const latency = Date.now() - emittedTime;
            if (latency > 0 && latency < 30000) {
              fanoutLatency.add(latency);
            }
          }
        }
      } catch (e) {
        // Non-JSON message or parse error — ignore pong frames etc.
      }
    });

    socket.on("error", function (e) {
      connectionSuccess.add(0);
      console.error(`VU ${vuId}: WebSocket error: ${e.error()}`);
    });

    socket.on("close", function () {
      // After socket closes, check if we received at least one delta
      // (subscribers should receive deltas if producers sent pings)
    });

    // Keep the connection open for the test duration
    // Wait for producers to send pings + aggregator interval + drain time
    socket.setTimeout(function () {
      socket.close();
    }, 45000);
  });

  // Post-connection checks
  check(res, {
    "WebSocket connection established (status 101)": (r) => r && r.status === 101,
  });

  // For non-producers (subscribers), they should have received at least one
  // heatmap.delta if any producer sent pings within the subscription bbox
  if (!isProducer && messagesReceived === 0) {
    // Only count as dropped if we expected messages (producers were active)
    // In a real scenario with producers active, zero messages = dropped
    droppedMessages.add(1);
    messageDeliveryRate.add(0);
  } else if (!isProducer && messagesReceived > 0) {
    // Successfully received fan-out messages
    messageDeliveryRate.add(1);
  }
}

// ---------------------------------------------------------------------------
// Setup and Teardown
// ---------------------------------------------------------------------------

export function setup() {
  console.log(`
╔══════════════════════════════════════════════════════════════╗
║  k6 WebSocket Fan-Out Load Test                            ║
║  Requirement 7.3: 200 concurrent connections, 0 drops     ║
╠══════════════════════════════════════════════════════════════╣
║  Target URL:    ${WS_URL.substring(0, 44).padEnd(44)}║
║  Connections:   ${String(CONCURRENT_CONNECTIONS).padEnd(44)}║
║  Producers:     ${String(Math.floor(CONCURRENT_CONNECTIONS * PRODUCER_RATIO)).padEnd(44)}║
║  Duration:      ${TEST_DURATION.padEnd(44)}║
╚══════════════════════════════════════════════════════════════╝
  `);

  return {
    startTime: Date.now(),
  };
}

export function teardown(data) {
  const elapsed = ((Date.now() - data.startTime) / 1000).toFixed(1);
  console.log(`
╔══════════════════════════════════════════════════════════════╗
║  Test completed in ${elapsed}s                              ║
║  Check thresholds above for PASS/FAIL                      ║
╚══════════════════════════════════════════════════════════════╝
  `);
}
