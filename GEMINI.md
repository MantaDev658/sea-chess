# GEMINI.md

## 1. Architectural Boundaries
- **Hexagonal Architecture**: The backend strictly adheres to a Ports and Adapters (Hexagonal) design. Core domain logic (`src/backend/Domain`, `src/backend/Application`) must remain completely isolated from infrastructure concerns (`src/backend/Infrastructure`, `src/backend/WebApi`).
- **Stack Definition**: F# (.NET) backend, React 19 / TypeScript frontend.
- **Data & State**: Postgres (persistence), Redis (background workers), SignalR (active game state).

## 2. Execution Behavior
- **Think Before Coding**: State assumptions explicitly. If the domain logic or problem space is ambiguous, halt and request clarification rather than guessing.
- **Simplicity First**: Implement the minimum code required to solve the explicit problem. Reject speculative features, over-abstraction, and unnecessary configurability.
- **Surgical Edits**: Modify only targeted files. Do not refactor adjacent code, alter pre-existing comments, or modify formatting outside the immediate task scope. Match existing styles exactly.
- **Goal-Driven Execution**: Define verifiable success criteria before modifying state. Loop independently until criteria are met.

## 3. Development Workflow & Verification
- **Zero-Warning Tolerance**: The build pipeline enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Any generated code must compile without F# warnings.
- **Style Enforcement**: Read and apply the root `.editorconfig` prior to writing F# code to prevent build failures driven by `EnforceCodeStyleInBuild`.
- **Mandatory Verification**: A task is never complete until the code builds successfully and all relevant tests pass. Use the available tooling continuously to verify state.
