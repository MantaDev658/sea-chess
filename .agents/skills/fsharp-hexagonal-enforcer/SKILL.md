# Skill: F# Hexagonal Architecture & Purity Enforcer
Description: Enforces functional purity, manual dependency injection, and Hexagonal boundaries for the F# monorepo.

## Context
This project utilizes a strict Hexagonal Architecture mapped to F# functional paradigms. Standard C# Object-Oriented patterns and standard ASP.NET Core Dependency Injection are explicitly prohibited.

## Rules & Constraints

### 1. Dependency Boundaries
| Layer | Allowed Dependencies | Prohibited Patterns |
| :--- | :--- | :--- |
| **Domain** | None. Pure F# only. | External NuGet packages, exceptions, mutable state, I/O. |
| **Application** | Domain. | Infrastructure libraries, DB connections, HTTP logic. |
| **Infrastructure** | Application, Npgsql.FSharp, StackExchange.Redis. | Domain logic implementation. |
| **WebApi** | Application, Infrastructure, Falco. | Standard MVC Controllers, `IServiceCollection` DI. |

### 2. Manual Dependency Injection
Never use standard ASP.NET Core IoC containers. All HTTP handlers must be pure functions that receive their dependencies (Ports) via partial application. 
If you modify a Use Case signature, you must synchronously update the composition root where the handler is partially applied.

### 3. Error Handling
Never throw exceptions for domain logic. Use F# `Result<T, Error>` types and ensure exhaustive pattern matching at the WebApi boundary to translate domain errors into exact HTTP status codes.

## Expected Patterns

### Defining and Fulfilling Ports
When creating a new external dependency, define a record or interface in the Application layer:

```fsharp
// Application/Ports.fs
type IMessageBus =
    abstract member PublishEvent: string -> Async<Result<unit, string>>
```

Fulfill it in the Infrastructure layer without modifying the Application layer:

```fsharp
// Infrastructure/RedisBus.fs
type RedisBus(connectionString) =
    interface IMessageBus with
        member this.PublishEvent(data) = async { ... }
```

### Handler Wiring
When wiring an endpoint, use partial application with Falco:
```fsharp
// WebApi/Handlers.fs
let myHandler (bus: IMessageBus) : HttpHandler =
    // Implementation
    
// WebApi/Program.fs
let bus = RedisBus("...")
let endpoints = [
    post "/api/event" (myHandler bus)
]
```
