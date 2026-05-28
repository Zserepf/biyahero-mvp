-- Migration: 004_create_routes
-- Description: Create routes and route_waypoints tables with PostGIS geospatial support
-- Requirements: 1.1, 1.2, 1.7, 1.8
--
-- This migration enables the PostGIS extension and creates the core routing tables.
-- The route_waypoints table uses a geometry(Point, 4326) column with a GiST index
-- to satisfy the Req 1.2 p95 ≤ 800ms bounding-box query target via ST_Intersects.
--
-- Dependencies:
--   - 001_create_enums.sql (vehicle_type, route_status enums)
--   - 002_create_users.sql (users table for FK references)

-- =============================================================================
-- PostGIS Extension
-- =============================================================================
-- Required for geometry(Point, 4326) column type and GiST spatial indexing.
-- On AWS RDS PostgreSQL, PostGIS is available as a shared library; this CREATE
-- EXTENSION call activates it in the current database.
CREATE EXTENSION IF NOT EXISTS postgis;

-- =============================================================================
-- Routes Table
-- =============================================================================
-- Stores community-sourced PUV routes. Each route is created with status
-- 'unverified' and transitions to 'verified' when a Moderator approves a revision.
-- The vehicle_type and route_status enums are defined in 001_create_enums.sql.

CREATE TABLE routes (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    name            text            NOT NULL,
    vehicle_type    vehicle_type    NOT NULL,
    status          route_status    NOT NULL DEFAULT 'unverified',
    created_by      uuid            NOT NULL REFERENCES users(id),
    base_fare       integer         NOT NULL CHECK (base_fare >= 0),
    created_at      timestamptz     NOT NULL DEFAULT now(),
    updated_at      timestamptz     NOT NULL DEFAULT now()
);

-- Index for querying routes by owner (user's submitted routes)
CREATE INDEX routes_owner_idx ON routes (created_by);

-- Index for filtering routes by verification status
CREATE INDEX routes_status_idx ON routes (status);

-- Auto-update updated_at on row modification (reuses function from 002_create_users)
CREATE TRIGGER trg_routes_updated_at
    BEFORE UPDATE ON routes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =============================================================================
-- Route Waypoints Table
-- =============================================================================
-- Stores the ordered sequence of geographic points that define a route.
-- The location column is a PostGIS geometry(Point, 4326) enabling spatial queries
-- via ST_Intersects on the GiST index. The latitude and longitude are also stored
-- as separate double precision columns for application-level access and as a
-- B-tree fallback path (see documentation below).

CREATE TABLE route_waypoints (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    route_id        uuid            NOT NULL REFERENCES routes(id) ON DELETE CASCADE,
    latitude        double precision NOT NULL,
    longitude       double precision NOT NULL,
    sequence_order  integer         NOT NULL,
    label           text,
    location        geometry(Point, 4326) NOT NULL,

    -- Enforce unique ordering within a route
    CONSTRAINT route_waypoints_route_position_unique UNIQUE (route_id, sequence_order)
);

-- GiST spatial index on the geometry column for fast bounding-box queries.
-- This is the primary query path: GET /v1/routes?bbox_sw=...&bbox_ne=... uses
-- ST_Intersects(location, ST_MakeEnvelope(sw_lng, sw_lat, ne_lng, ne_lat, 4326))
-- to find all waypoints (and their parent routes) within the requested area.
CREATE INDEX route_waypoints_location_gix ON route_waypoints USING gist (location);

-- B-tree index on route_id for fast joins back to the routes table
CREATE INDEX route_waypoints_route_idx ON route_waypoints (route_id);

-- =============================================================================
-- Lat/Lng B-Tree Fallback Documentation
-- =============================================================================
-- If PostGIS cannot be enabled on the target RDS engine (e.g., Aurora Serverless
-- v1 without PostGIS, or a non-RDS Postgres instance without the extension),
-- the schema can fall back to querying the separate latitude/longitude columns
-- with a composite B-tree index.
--
-- FALLBACK INDEX (uncomment if PostGIS is unavailable):
--
--   CREATE INDEX route_waypoints_lat_lng_btree_idx
--       ON route_waypoints (latitude, longitude);
--
-- FALLBACK QUERY PATTERN:
--
--   SELECT DISTINCT r.*
--   FROM routes r
--   JOIN route_waypoints rw ON rw.route_id = r.id
--   WHERE rw.latitude BETWEEN @sw_lat AND @ne_lat
--     AND rw.longitude BETWEEN @sw_lng AND @ne_lng;
--
-- TRADE-OFFS:
--   - A composite B-tree index uses only the leading column (latitude) for the
--     initial range scan. The planner reads all rows in the latitude band and
--     then filter-evaluates the longitude predicate — effectively a partial index
--     scan plus a filter step.
--   - At MVP scale (< 10k waypoints) this is acceptable and meets the 800ms p95
--     target. However, performance degrades sub-linearly as waypoint count grows:
--     at > 100k waypoints the latitude-band scan becomes a measurable bottleneck.
--   - PostGIS GiST is strongly preferred for nationwide growth because it indexes
--     both dimensions simultaneously and supports true spatial predicates
--     (ST_Intersects, ST_DWithin, ST_Contains) with logarithmic lookup cost.
--
-- CONDITIONAL PATH:
--   The application repository layer (RouteRepository) should detect whether
--   PostGIS is available at startup (e.g., SELECT PostGIS_Version()) and choose
--   the appropriate query strategy:
--     - PostGIS available → use ST_Intersects with the GiST index
--     - PostGIS unavailable → use lat/lng BETWEEN with the B-tree index
-- =============================================================================
