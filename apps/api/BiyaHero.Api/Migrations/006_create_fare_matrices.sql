-- Migration: 006_create_fare_matrices
-- Description: Create fare_matrices table and seed LTFRB v1 fare data
-- Requirements: 2.1, 2.3, 2.4, 2.5, 2.8

CREATE TABLE fare_matrices (
    id                              uuid            PRIMARY KEY DEFAULT gen_random_uuid(),
    version                         text            NOT NULL,
    effective_at                    timestamptz     NOT NULL,
    vehicle_type                    vehicle_type    NOT NULL,
    min_fare_centavos               integer         NOT NULL CHECK (min_fare_centavos > 0),
    min_fare_km                     double precision NOT NULL CHECK (min_fare_km > 0),
    per_km_centavos                 integer         NOT NULL CHECK (per_km_centavos > 0),
    discount_percent_by_category    jsonb           NOT NULL DEFAULT '{}',
    created_at                      timestamptz     NOT NULL DEFAULT now(),

    CONSTRAINT fare_matrices_version_vehicle_unique UNIQUE (version, vehicle_type)
);

-- Index for loading the active matrix by version
CREATE INDEX fare_matrices_version_idx ON fare_matrices (version);

-- Index for querying by effective date (latest active matrix)
CREATE INDEX fare_matrices_effective_at_idx ON fare_matrices (effective_at DESC);

-- Seed: LTFRB v1 fare matrix (effective 2024-01-01)
-- Values sourced from fare-matrix.json matching LTFRB-published rates

INSERT INTO fare_matrices (version, effective_at, vehicle_type, min_fare_centavos, min_fare_km, per_km_centavos, discount_percent_by_category)
VALUES
    (
        'v1-2024',
        '2024-01-01T00:00:00Z',
        'jeepney',
        1300,
        4.0,
        180,
        '{"regular": 0, "student": 20, "senior": 20, "pwd": 20}'
    ),
    (
        'v1-2024',
        '2024-01-01T00:00:00Z',
        'bus',
        1500,
        5.0,
        265,
        '{"regular": 0, "student": 20, "senior": 20, "pwd": 20}'
    ),
    (
        'v1-2024',
        '2024-01-01T00:00:00Z',
        'uv_express',
        3000,
        5.0,
        200,
        '{"regular": 0, "student": 20, "senior": 20, "pwd": 20}'
    ),
    (
        'v1-2024',
        '2024-01-01T00:00:00Z',
        'tricycle',
        4000,
        2.0,
        500,
        '{"regular": 0, "student": 20, "senior": 20, "pwd": 20}'
    );
