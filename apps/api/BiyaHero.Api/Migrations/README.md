# Database Migrations

Sequential PostgreSQL migration files for the BiyaHero API.

## Running Migrations

Execute migration files in numerical order against your PostgreSQL database:

```bash
psql -h <host> -U <user> -d biyahero -f 001_create_enums.sql
psql -h <host> -U <user> -d biyahero -f 002_create_users.sql
psql -h <host> -U <user> -d biyahero -f 003_create_audit_log.sql
psql -h <host> -U <user> -d biyahero -f 004_create_routes.sql
psql -h <host> -U <user> -d biyahero -f 006_create_fare_matrices.sql
```

Or using the Docker Compose test harness:

```bash
docker compose -f docker-compose.test.yml exec postgres psql -U postgres -d biyahero -f /migrations/001_create_enums.sql
```

## Prerequisites

- PostgreSQL 16+ with the `citext` extension available
- PostGIS extension (required by later migrations for geospatial queries)

## Naming Convention

Files follow the pattern `NNN_description.sql` where `NNN` is a zero-padded sequence number. Always add new migrations at the next available number.
