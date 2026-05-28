-- Migration: 005_create_revisions_votes
-- Description: Create route_revisions, route_revision_waypoints, and route_votes tables
-- Requirements: 1.3, 1.4, 1.5
--
-- route_revisions: Pending edits submitted by commuters, linked to the original route.
-- route_revision_waypoints: Waypoints belonging to a revision (separate from verified route waypoints).
-- route_votes: Accuracy votes cast by commuters on verified routes (one vote per user per route).

-- ============================================================================
-- route_revisions
-- ============================================================================
CREATE TABLE route_revisions (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    route_id        uuid            NOT NULL REFERENCES routes(id),
    submitted_by    uuid            NOT NULL REFERENCES users(id),
    status          revision_status NOT NULL DEFAULT 'pending',
    approver_id     uuid            REFERENCES users(id),
    created_at      timestamptz     NOT NULL DEFAULT now(),
    updated_at      timestamptz     NOT NULL DEFAULT now()
);

-- Index for querying revisions by route (e.g., listing pending revisions for a route)
CREATE INDEX route_revisions_route_idx ON route_revisions (route_id);

-- Index for querying revisions by submitter
CREATE INDEX route_revisions_submitter_idx ON route_revisions (submitted_by);

-- Index for filtering by status (e.g., moderator queue of pending revisions)
CREATE INDEX route_revisions_status_idx ON route_revisions (status);

-- Auto-update updated_at on row modification
CREATE TRIGGER trg_route_revisions_updated_at
    BEFORE UPDATE ON route_revisions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- route_revision_waypoints
-- ============================================================================
CREATE TABLE route_revision_waypoints (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    revision_id     uuid            NOT NULL REFERENCES route_revisions(id) ON DELETE CASCADE,
    latitude        double precision NOT NULL,
    longitude       double precision NOT NULL,
    sequence_order  integer         NOT NULL,
    label           text,

    CONSTRAINT route_revision_waypoints_order_unique UNIQUE (revision_id, sequence_order)
);

-- Index for querying waypoints by revision
CREATE INDEX route_revision_waypoints_revision_idx ON route_revision_waypoints (revision_id);

-- ============================================================================
-- route_votes
-- ============================================================================
CREATE TABLE route_votes (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    route_id        uuid            NOT NULL REFERENCES routes(id),
    voter_id        uuid            NOT NULL REFERENCES users(id),
    kind            vote_kind       NOT NULL,
    created_at      timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT route_votes_one_per_user UNIQUE (route_id, voter_id)
);

-- Index for querying votes by route (e.g., counting accuracy votes)
CREATE INDEX route_votes_route_idx ON route_votes (route_id);

-- Index for querying votes by voter
CREATE INDEX route_votes_voter_idx ON route_votes (voter_id);
