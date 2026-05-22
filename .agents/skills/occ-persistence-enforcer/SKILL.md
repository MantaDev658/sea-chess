# Skill: Optimistic Concurrency Control (OCC) Enforcer
Description: Enforces strict version-based database persistence and explicit 409 Conflict handling across all layers.

## Context
This project rejects implicit ORM concurrency management in favor of explicit Optimistic Concurrency Control (OCC) using pure F# types, `Npgsql.FSharp`, and raw SQL. State mutation requires strict version tracking to prevent race conditions in real-time environments.

## Rules & Constraints

### 1. The Version Invariant
Every writable aggregate or state representation stored in the database must include an integer `Version` field.

### 2. Application Layer Ports
Repository interfaces (Ports) that perform updates MUST require an `expectedVersion` parameter.
```fsharp
// Application/Ports.fs
type IGameRepository =
    abstract member SaveGame: gameId: string * state: GameState * expectedVersion: int -> Async<Result<unit, SaveGameError>>
```

### 3. Infrastructure Layer SQL Execution
All `UPDATE` queries must explicitly include the version check in the `WHERE` clause and increment the version in the `SET` clause. If no rows are affected, it indicates a concurrency conflict or missing record.
```fsharp
// Infrastructure/PostgresRepo.fs
let sql = "UPDATE Games SET StateFen = @fen, Version = Version + 1 WHERE GameId = @id AND Version = @expectedVersion"
// ... execute query ...
if recordsAffected = 0 then
    Error SaveGameError.ConcurrencyConflict
else
    Ok ()
```

### 4. Web API Boundary (HTTP 409)
The Web API layer must aggressively match concurrency errors and translate them strictly to `HTTP 409 Conflict`. Standard `400 Bad Request` or `500 Internal Server Error` statuses are prohibited for versioning conflicts.
```fsharp
// WebApi/Handlers.fs
match result with
| Error SaveGameError.ConcurrencyConflict -> 
    Response.withStatusCode 409 >> Response.ofPlainText "Concurrency conflict: The state has been modified by another process."
```

## Failure Points to Monitor
- **Blind Overwrites:** Standard agents will default to `UPDATE Table SET Value = @v WHERE Id = @id`. This skill prohibits that pattern.
- **Silent Failures:** Forgetting to check the row count (`recordsAffected`) after a parameterized update query allows the system to continue as if the save succeeded, corrupting in-memory state versus persistent state.

