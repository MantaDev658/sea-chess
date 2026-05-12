namespace ChessPlatform.Application

open ChessPlatform.Domain

/// A wrapper around GameState to track user IDs, preserving Domain purity.
type GameRecord = {
    GameId: string
    State: GameState
    WhitePlayerId: string
    BlackPlayerId: string
}

type SaveGameError =
    | ConcurrencyConflict
    | PersistenceFailure of string

/// Port for database access.
type IGameRepository =
    /// Loads a game by ID.
    abstract member LoadGame: gameId: string -> Async<GameRecord option>
    
    /// Saves a game state, enforcing Optimistic Concurrency Control (OCC) using expectedVersion.
    abstract member SaveGame: gameId: string * state: GameState * expectedVersion: int -> Async<Result<unit, SaveGameError>>

/// Port for publishing events to the message bus (Redis).
type IMessageBus =
    /// Publishes an event indicating a move was played.
    abstract member PublishMovePlayed: gameId: string * move: Move -> Async<unit>
    
    /// Adds a player to the matchmaking queue.
    abstract member EnqueueMatchmaking: userId: string * elo: int -> Async<unit>
