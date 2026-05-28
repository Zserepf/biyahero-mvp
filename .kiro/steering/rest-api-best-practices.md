# REST API Best Practices

Follow these standards when designing and building REST APIs across all backend projects.

---

## API Definition

- All APIs must be defined using **OpenAPI 3.0**

---

## URL Design

- All paths must be **kebab-case** (e.g., `product-categories`)
- URLs must be centered around **resources**, not actions (e.g., `products`, `collections`, `checkouts`)
- Resource names must be **plural** (e.g., `/products`, not `/product`)
- A consistent **API vocabulary** must be maintained across the entire API

### Versioning

A clear versioning strategy must be in place using the format:

```
/<major-version>/<subsystem>/<resource>
```

Examples:
```
/inventory/v1/
/v1/inventory/products?q=search-term
```

---

## Standard Methods vs. Custom Methods

### Standard Methods (preferred)

Standard REST methods are preferred over custom methods. They must have **no side effects** — if a side effect is required, use a custom method instead.

| Method | Example |
|--------|---------|
| `POST` | `/checkouts` |
| `GET` | `/checkouts/123` |
| `PATCH` | `/checkouts/123` |
| `PUT` | `/checkouts/123` |
| `DELETE` | `/checkouts/123` |

### Custom Methods

Use custom methods for specific operations that don't fit standard CRUD.

- Must be indicated by a **colon `:`** in the URL
- Must always use the **`POST`** HTTP method

Examples:
```
POST /checkouts/:filter_order
POST /checkouts/134/:complete
```

---

## HTTP Status Codes

Use HTTP status codes expressively and consistently.

### 1xx
- `100` — Operation in progress

### 2xx — Success
- `200` — OK
- `201` — Created
- `204` — No Content (use for `DELETE` responses)

### 4xx — Client Errors
- `400` — Generic request error
- `401` — Unauthenticated (missing or invalid credentials)
- `403` — Unauthorized / Forbidden (valid credentials, insufficient permissions)
- `404` — Not Found
- `405` — Method Not Allowed (method is not available and there are no plans to implement it)
- `409` — Conflict (object can't be created because it conflicts with an existing one)
- `422` — Unprocessable Entity / Params Error

### 5xx — Server Errors
- `500` — Generic server error
- `501` — Not Implemented (method not available but will be implemented in the future)
