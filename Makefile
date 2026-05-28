.PHONY: test-harness-up test-harness-down test-integration

## Boot the test harness (PostgreSQL 16 + PostGIS, DynamoDB Local)
test-harness-up:
	@bash scripts/test-harness.sh

## Tear down the test harness
test-harness-down:
	@bash scripts/test-harness.sh --down

## Boot harness, run integration tests, then tear down
test-integration:
	@bash scripts/test-harness.sh --run
