# Python Flask Backend — Feature-Sliced Architecture Guide

## Philosophy

Slice by **endpoint/feature**, not by domain or technical layer. Each feature folder maps to a specific API operation (e.g., `create_user`, `get_product_by_id`) and owns exactly three things: the endpoint definition, the business logic handler, and the schemas.

Everything else — models, repositories, shared utilities — lives outside feature folders in their proper homes.

- Each feature is a single, self-contained unit of work. Open the folder, see everything that endpoint does.
- Adding a new endpoint means adding a new folder. You never modify existing feature files.
- Repositories and models are shared resources — they don't belong to any single feature.
- Tests mirror features 1:1 in a dedicated `tests/` folder.

> One endpoint, one folder. Shared resources live in shared places.

---

## Directory Structure

```
src/
├── app.py                        # App factory
├── extensions.py                 # Extension instances (db, ma, migrate, etc.)
├── config.py                     # Environment-based config classes
│
├── features/
│   └── <subsystem>/
│       └── <operation>/          # One folder per API endpoint
│           ├── __init__.py
│           ├── endpoint.py       # Thin HTTP boundary
│           ├── handler.py        # Business logic
│           └── schemas.py        # Input validation + response serialization
│
├── domain/
│   └── <model>.py                # Pure SQLAlchemy model definitions only
│
├── infrastructure/
│   ├── base_repository.py        # Generic CRUD base repository
│   ├── base_model.py             # Timestamp mixin
│   └── repositories/
│       └── <entity>_repository.py  # Entity-specific queries
│
├── common/
│   ├── exceptions.py             # Custom exception classes
│   ├── middleware.py             # Global exception handler
│   ├── responses.py              # Standardized response helpers
│   └── utils/                    # Truly shared utilities
│
└── tests/
    ├── conftest.py
    └── features/                 # Mirrors the features/ structure
        └── <subsystem>/
            └── test_<operation>.py
```

---

## Feature Slice Anatomy

Each feature = one API operation. Three files, clear responsibilities.

### `endpoint.py` — Thin HTTP Boundary

Parse the request, delegate to the handler, return the response. Nothing else.

- Call `schema.load()` for input validation
- Call the handler
- Call `schema.dump()` for the response
- No `if` logic, no `db.session`, no business decisions

### `handler.py` — Business Logic

Where decisions are made. The handler calls repositories, validates business rules that require DB state (uniqueness, authorization, cross-field logic involving DB lookups), and orchestrates the operation.

- Handlers call repository methods — never raw queries
- Handlers never re-validate what schemas already enforce
- Handlers never call other handlers — use repositories or `common/utils/` for shared logic

### `schemas.py` — Input Validation AND Serialization

Single source of truth for both validating incoming data and shaping outgoing responses. Uses Marshmallow.

- `RequestSchema` — defines required fields, types, constraints, and custom validators. Calling `.load()` in the endpoint both deserializes and validates. On failure, Marshmallow raises `ValidationError` which the global handler catches as a `400`.
- `ResponseSchema` — defines what gets serialized back to the client via `.dump()`

| What | Where | Example |
|------|-------|---------|
| Type checking, required fields, format | `schemas.py` via Marshmallow | email format, string length |
| Custom field-level rules | `schemas.py` via `@validates` | whitespace-only rejection |
| Cross-field validation (input only) | `schemas.py` via `@validates_schema` | end_date must be after start_date |
| Business rules requiring DB state | `handler.py` | email uniqueness, entity existence |
| Response shaping | `schemas.py` via response schema | which fields to expose, `dump_only` |

---

## Domain Layer — Models Only

Pure SQLAlchemy definitions. No business logic, no queries, no validation. These are shared data structures that multiple features reference.

- No methods on models
- No validation on models
- No business logic on models

---

## Infrastructure Layer

### `base_repository.py`
Generic CRUD operations shared across all repositories (`find_by_id`, `all`, `save`, `delete`, etc.).

### `repositories/<entity>_repository.py`
Extends `BaseRepository`. Only adds queries unique to that entity.

- Repositories are the **only** files that touch `db.session`

### `base_model.py`
Timestamp mixin (`created_at`, `updated_at`) inherited by all domain models.

---

## Common Layer

### `exceptions.py`
Custom exception classes. Features raise exceptions — they never catch them.

### `middleware.py`
Global exception handler. All exceptions bubble up here. Features never handle their own exceptions.

### `responses.py`
Standardized response shape helpers used across all endpoints.

---

## How to Add a New Feature

Example: adding `POST /api/v1/orders`:

1. Create `src/features/orders/create_order/` with `__init__.py`, `endpoint.py`, `handler.py`, `schemas.py`
2. In `schemas.py`: define `CreateOrderRequest` (input validation) and `CreateOrderResponse` (serialization)
3. If the `Order` model doesn't exist yet, add `src/domain/order.py`
4. If `OrderRepository` doesn't exist yet, add `src/infrastructure/repositories/order_repository.py` extending `BaseRepository[Order]`
5. Write the handler with business logic, calling the repository
6. Wire up the endpoint (thin — load schema, call handler, dump response)
7. Register the blueprint in `app.py` → `_register_features()`
8. Run `flask db migrate -m "add orders table"` and `flask db upgrade`
9. Add `tests/features/orders/test_create_order.py`

You never touch existing feature files.

---

## Best Practices Checklist

1. One feature folder = one endpoint (`create_user`, `list_users`, `update_user` are separate folders)
2. Endpoints are dumb — parse request, call `.load()`, call handler, call `.dump()`. No `if` logic, no `db.session`
3. Schemas own ALL input validation — if it doesn't need a DB call, it belongs in the schema
4. Handlers own business rules that require state — uniqueness checks, authorization, DB lookups
5. Never duplicate validation — if the schema checks email format, the handler doesn't check it again
6. Schemas are feature-scoped — `CreateUserRequest` and `UpdateUserRequest` are separate even if similar
7. Repositories are the only files that touch `db.session`
8. Base repository handles generic CRUD — entity repos only add custom queries
9. Domain models are plain SQLAlchemy — no methods, no validation, no business logic
10. Exceptions are raised, never caught in features — the global middleware handles everything
11. Cross-feature calls go through repositories, not handlers
12. Tests mirror features 1:1 in the `tests/` directory

---

## Anti-Patterns to Avoid

- Fat endpoints — business logic or validation in `endpoint.py`
- Validation in handlers that schemas should own — if it doesn't need a DB call, put it in the schema
- Shared schemas across features — each feature's schemas evolve independently
- Handlers calling other handlers — use repositories or `common/utils/` instead
- Models with methods — keep domain models as pure data definitions
- Catching exceptions in features — let them bubble up to the global handler
- Putting tests inside feature folders — tests live in `tests/` only
