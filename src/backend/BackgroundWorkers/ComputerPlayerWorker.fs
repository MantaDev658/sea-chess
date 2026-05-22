namespace ChessPlatform.BackgroundWorkers

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Threading
open ChessPlatform.Application
open ChessPlatform.Domain
open Engine

type ComputerPlayerWorker(
    logger: ILogger<ComputerPlayerWorker>,
    repo: IGameRepository,
    bus: IMessageBus,
    engine: IComputerEngine) =
    
    inherit BackgroundService()

    let computerUserId = "computer"

    let handleMovePlayed (gameId: string) = async {
        try
            logger.LogInformation("Received move event for game {GameId}", gameId)
            
            let! gameOpt = repo.LoadGame gameId
            match gameOpt with
            | None -> ()
            | Some record ->
                let expectedUserId = 
                    match record.State.ActiveColor with
                    | White -> record.WhitePlayerId
                    | Black -> record.BlackPlayerId
                    
                if expectedUserId = computerUserId then
                    logger.LogInformation("Computer's turn in game {GameId}. Thinking...", gameId)
                    let! bestMoveOpt = engine.GetBestMove record.State
                    match bestMoveOpt with
                    | None -> 
                        logger.LogWarning("Computer has no legal moves in game {GameId}", gameId)
                    | Some moveReq ->
                        do! Async.Sleep 500
                        let! result = PlayMoveUseCase.execute repo bus gameId computerUserId moveReq
                        match result with
                        | Ok _ -> logger.LogInformation("Computer move successful for game {GameId}", gameId)
                        | Error e -> logger.LogError("Computer move failed in game {GameId}: {Error}", gameId, e)
        with ex ->
            logger.LogError(ex, "Error handling move event for game {GameId}", gameId)
    }

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        let tcs = new System.Threading.Tasks.TaskCompletionSource<unit>()
        stoppingToken.Register(fun () -> tcs.SetResult()) |> ignore
        
        let startSubscription() = async {
            logger.LogInformation("ComputerPlayerWorker starting subscription...")
            let! disposable = bus.SubscribeMovePlayed(handleMovePlayed)
            
            let! _ = Async.AwaitTask tcs.Task
            
            disposable.Dispose()
            logger.LogInformation("ComputerPlayerWorker stopped.")
        }
        
        Async.StartAsTask(startSubscription(), cancellationToken = stoppingToken) :> System.Threading.Tasks.Task
