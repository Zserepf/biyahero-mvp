-- Migration: 002_create_users
-- Description: Create users table with language preference and indexes
-- Requirements: 5.1, 5.7, 10.1

CREATE TABLE users (
    id              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    email           citext          NOT NULL,
    password_hash   text            NOT NULL,
    role            user_role       NOT NULL DEFAULT 'commuter',
    status          user_status     NOT NULL DEFAULT 'pending_verification',
    display_name    text            NOT NULL,
    language_preference char(3)     NOT NULL DEFAULT 'fil'
                                    CHECK (language_preference IN ('en', 'fil')),
    created_at      timestamptz     NOT NULL DEFAULT now(),
    updated_at      timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT users_email_unique UNIQUE (email)
);

-- Index for fast email lookups (login, registration conflict check)
CREATE INDEX users_email_idx ON users (email);

-- Trigger to auto-update updated_at on row modification
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
