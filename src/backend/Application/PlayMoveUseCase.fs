namespace ChessPlatform.Application

open ChessPlatform.Domain

module PlayMoveUseCase =

    /// Defines errors that can occur during the PlayMove use case.
    type PlayMoveError =
        | GameNotFound
        | NotYourTurn
        | DomainError of MoveError
        | ConcurrencyConflict
        | PersistenceError of string

    /// Orchestrates the process of playing a move.
    let execute 
        (repo: IGameRepository) 
        (bus: IMessageBus) 
        (gameId: string) 
        (userId: string) 
        (request: MoveRequest) 
        : Async<Result<GameState, PlayMoveError>> = 
        async {
            // 1. Load the game
            let! gameOpt = repo.LoadGame gameId
            match gameOpt with
            | None -> return Error GameNotFound
            | Some record ->
                // 2. Validate it's the correct user's turn
                let expectedUserId = 
                    match record.State.ActiveColor with
                    | White -> record.WhitePlayerId
                    | Black -> record.BlackPlayerId
                
                if userId <> expectedUserId then
                    return Error NotYourTurn
                else
                    // 3. Play the move via the Domain
                    match Game.playMove record.State request with
                    | Error domainErr -> return Error (DomainError domainErr)
                    | Ok newState ->
                        // 4. Extract the actual Move that was played (from the history)
                        let playedMove = List.head newState.History
                        
                        // 5. Save the state with OCC
                        let! saveResult = repo.SaveGame(gameId, newState, record.State.Version)
                        match saveResult with
                        | Error SaveGameError.ConcurrencyConflict -> 
                            return Error ConcurrencyConflict
                        | Error (SaveGameError.PersistenceFailure msg) -> 
                            return Error (PersistenceError msg)
                        | Ok () ->
                            // 6. Publish the event
                            do! bus.PublishMovePlayed(gameId, playedMove)
                            return Ok newState
        }
