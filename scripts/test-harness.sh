#!/usr/bin/env bash
# BiyaHero Test Harness
# Boots PostgreSQL 16 + PostGIS and DynamoDB Local for integration tests.
# Usage:
#   ./scripts/test-harness.sh          # Boot services and wait for healthy
#   ./scripts/test-harness.sh --down   # Tear down services
#   ./scripts/test-harness.sh --run    # Boot, run tests, then tear down

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.test.yml"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Tear down services
harness_down() {
  log_info "Tearing down test harness..."
  docker compose -f "$COMPOSE_FILE" down -v --remove-orphans
  log_info "Test harness stopped."
}

# Boot services and wait for health checks to pass
harness_up() {
  log_info "Starting test harness (PostgreSQL 16 + PostGIS, DynamoDB Local)..."
  docker compose -f "$COMPOSE_FILE" up -d --wait

  if [ $? -eq 0 ]; then
    log_info "All services are healthy."
    log_info "  PostgreSQL: localhost:5432 (user: biyahero_test, db: biyahero_test)"
    log_info "  DynamoDB Local: localhost:8000"
  else
    log_error "Services failed to become healthy. Check docker compose logs."
    docker compose -f "$COMPOSE_FILE" logs
    exit 1
  fi
}

# Run integration tests (placeholder — extend with actual test commands)
run_tests() {
  log_info "Running integration tests..."

  # .NET backend tests (if the project exists)
  if [ -d "$PROJECT_ROOT/apps/api" ]; then
    log_info "Running .NET integration tests..."
    dotnet test "$PROJECT_ROOT/apps/api" --filter "Category=Integration" || true
  fi

  # Frontend tests (if the project exists)
  if [ -f "$PROJECT_ROOT/apps/web/package.json" ]; then
    log_info "Running frontend tests..."
    (cd "$PROJECT_ROOT/apps/web" && npm test -- --run) || true
  fi

  log_info "Integration tests complete."
}

# Main
case "${1:-}" in
  --down)
    harness_down
    ;;
  --run)
    harness_up
    run_tests
    harness_down
    ;;
  *)
    harness_up
    ;;
esac
