namespace ChessPlatform.Application

module MatchmakingUseCase =

    /// Orchestrates adding a player to the matchmaking queue.
    let enqueue (bus: IMessageBus) (userId: string) (elo: int) : Async<unit> =
        async { do! bus.EnqueueMatchmaking (userId, elo) }
