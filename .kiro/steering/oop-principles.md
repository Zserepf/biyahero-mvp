# Object-Oriented Programming Principles

Follow these OOP principles across all projects to ensure consistent, maintainable, and well-structured code.

---

## Principle 1: Follow Proper Layering

Code must be organized in the following layers, in order:

- `serverless.yml` — lists all lambda functions, points code to a handler
- `handler` — the "controller"; the first entrypoint of code
- `domain` — where business logic lives
- `base_domain` — inherited by domain; contains common repeated functions like `serialize()` and `__init__(self, data)`
- `repository` — where data logic lives; decouples data logic from business logic (critical for TDD)
  - `base_mongodb_repository` — generic data logic for MongoDB
- `service` — low-level code that interfaces with the data store; the **only** place where `boto3` (or equivalent SDK) code should appear

---

## Principle 2: Don't Use Classes as Function Containers

Classes must represent real-world objects, not just group related functions.

**Bad patterns:**
- Purely imperative style — state passed around at the top-level handler scope
- Classes used only as code containers with no real instantiation or instance state

**Good pattern — classes emulate real-world objects:**
- Create separate classes for each entity (e.g., `Customer`, `Order`, `LineItem`)
- Each class has an `__init__` method that stores data on the instance
- Database operations are abstracted to a `ModelRepository` / `BaseRepository`

**You are probably doing OOP wrong if:**
- You are using too many `@classmethod`s
- Your class name contains a verb

> RULE 1: Never expose database-related syntax at the domain level.

---

## Principle 3: Standard Methods for OOP

All models must follow this standard method interface so code looks and feels consistent across projects.

```python
# Class methods
Model.find()      # returns one instance
Model.find_all()  # returns all results (can be paginated)
Model.create()    # returns the created instance
Model.where()     # returns filtered results (can be paginated)

# Instance methods
obj.update()
obj.save()
obj.delete()
obj.serialize()   # returns a JSON representation
```

### Class Method Rules
- Must use `cls` as the first argument
- Must include the `@classmethod` decorator
- Act on the model as a group (finding, listing, creating)

### Instance Method Rules
- Must use `self` as the first argument
- Act on a single instance (serialize, delete, update, save)

### Additional Rules
- `find` — given a key, returns an instance of the found object
- `create` — creates the element, returns an instance
- `all` — returns a list of instantiated objects
- `serialize` — returns a JSON dict
- Offload generic/shared methods to a base class (e.g., `DynamodbModelBase`)
- Class methods must **never** return raw JSON — always return an instance of the object

---

## Principle 4: Avoid `if` Statements — Use Lookup Hashes Instead

Replace chains of `if`/`elif` statements with lookup dictionaries (hashes) where possible. This reduces branching complexity and makes the code easier to extend.

```python
# Instead of:
if status == "active":
    handler = handle_active
elif status == "inactive":
    handler = handle_inactive

# Do this:
STATUS_HANDLERS = {
    "active": handle_active,
    "inactive": handle_inactive,
}
handler = STATUS_HANDLERS[status]
```

---

## Principle 5: Object Composition

Avoid cramming all functionality into one large model. Instead, compose objects from smaller, focused sub-objects.

- All domain classes should inherit from `BaseDomain`
- Decompose complex objects into their component parts, for example a `Cart` should be composed of:
  - `Customer` (with `BillingAddress`, `ShippingAddress`)
  - List of `CartItem` objects
- Each sub-object is responsible for its own logic, keeping individual files small and focused

---

## Principle 6: Use Exceptions to Break Flow

Instead of deeply nested `if` statements to handle invalid states, raise exceptions early.

```python
# Instead of:
if not user:
    return error_response("User not found")

# Do this:
if not user:
    raise UserNotFoundException("User not found")
```

- Raise specific, named exceptions to signal invalid states
- Let exception handlers at a higher layer deal with the response formatting
- This keeps domain logic clean and free of response-handling concerns

---

## Principle 7: Database Tables ≠ OOP Class Entities

Database tables and OOP class entities serve different purposes and should not be treated as the same thing.

- A database table is a storage concern
- An OOP class entity is a business/domain concern
- Do not model your classes 1:1 with your database schema — model them around your domain concepts
- Data transformation between the two is the responsibility of the `repository` layer
