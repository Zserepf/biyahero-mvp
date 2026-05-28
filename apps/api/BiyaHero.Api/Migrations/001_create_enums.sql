-- Migration: 001_create_enums
-- Description: Create PostgreSQL enum types for BiyaHero domain values
-- Requirements: 5.1, 5.7, 10.1

-- Enable citext extension for case-insensitive email storage
CREATE EXTENSION IF NOT EXISTS citext;

-- User roles: Commuter, Driver, Moderator, SuperAdmin
CREATE TYPE user_role AS ENUM (
    'commuter',
    'driver',
    'moderator',
    'super_admin'
);

-- User account lifecycle statuses
CREATE TYPE user_status AS ENUM (
    'pending_verification',
    'active',
    'suspended'
);

-- Public utility vehicle types supported by BiyaHero
CREATE TYPE vehicle_type AS ENUM (
    'jeepney',
    'bus',
    'uv_express',
    'tricycle'
);

-- Verification status of a community-sourced route
CREATE TYPE route_status AS ENUM (
    'unverified',
    'verified'
);

-- Status of a route revision submitted by a commuter
CREATE TYPE revision_status AS ENUM (
    'pending',
    'approved',
    'rejected'
);

-- Kind of accuracy vote a commuter can cast on a verified route
CREATE TYPE vote_kind AS ENUM (
    'still_accurate',
    'no_longer_accurate'
);
