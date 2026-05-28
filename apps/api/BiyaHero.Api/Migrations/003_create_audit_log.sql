-- Migration: 003_create_audit_log
-- Description: Create audit_log table for Super Admin, Auth, and Payment writes
-- Requirements: 5.10, 8.5
--
-- This table is the queryable system-of-record for audit events.
-- Entries are also mirrored to a CloudWatch log group (30-day retention)
-- as the immutable append-only sink.

CREATE TABLE audit_log (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id        uuid            NOT NULL REFERENCES users(id),
    action          text            NOT NULL,
    target_type     text            NOT NULL,
    target_id       uuid,
    occurred_at     timestamptz     NOT NULL DEFAULT now(),
    metadata        jsonb
);

-- Index for querying audit entries by actor (e.g., Super Admin activity review)
CREATE INDEX audit_log_actor_idx ON audit_log (actor_id);

-- Index for querying audit entries by target resource
CREATE INDEX audit_log_target_idx ON audit_log (target_type, target_id);

-- Index for time-range queries (recent activity, retention policies)
CREATE INDEX audit_log_occurred_at_idx ON audit_log (occurred_at);
