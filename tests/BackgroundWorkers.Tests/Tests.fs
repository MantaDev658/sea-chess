module Tests

open Expecto
open ChessPlatform.Domain
open ChessPlatform.Application
open ChessPlatform.BackgroundWorkers
open ChessPlatform.BackgroundWorkers.Engine
open Microsoft.Extensions.Logging.Abstractions

// Helper to unwrap Results in tests
let unwrap =
    function
    | Ok x -> x
    | Error e -> failwithf "Expected Ok, got Error: %A" e

// Mock implementation of IMessageBus
type MockMessageBus() =
    let mutable subscribeHandler = None
    let mutable publishedMoves = []

    member _.SubscribeHandler = subscribeHandler

    member _.PublishedMoves = publishedMoves

    interface IMessageBus with
        member _.PublishMovePlayed(gameId, move) =
            async { publishedMoves <- (gameId, move) :: publishedMoves }

        member _.EnqueueMatchmaking(_, _) = async { () }

        member _.SubscribeMovePlayed(handler) =
            async {
                subscribeHandler <- Some handler

                return
                    { new System.IDisposable with
                        member _.Dispose() = ()
                    }
            }

// Helper to wait for the worker to subscribe deterministically
let waitForSubscription (bus: MockMessageBus) =
    async {
        let mutable handler = None

        while Option.isNone handler do
            do! Async.Sleep 10
            handler <- bus.SubscribeHandler

        return Option.get handler
    }

// Mock implementation of IGameRepository
type MockGameRepository(initialGames: Map<string, GameRecord>) =
    let mutable games = initialGames
    let mutable savedRecords = []

    member _.Games = games
    member _.SavedRecords = savedRecords

    interface IGameRepository with
        member _.LoadGame(gameId) =
            async { return Map.tryFind gameId games }

        member _.SaveGame(record, expectedVersion) =
            async {
                let currentRecord = games.[record.GameId]

                if currentRecord.State.Version <> expectedVersion then
                    return Error SaveGameError.ConcurrencyConflict
                else
                    games <- Map.add record.GameId record games
                    savedRecords <- record :: savedRecords
                    return Ok ()
            }

        member _.CreateGame(record) =
            async {
                games <- Map.add record.GameId record games
                return Ok ()
            }

// Mock implementation of IComputerEngine
type MockComputerEngine(bestMoveToReturn: MoveRequest option) =
    let mutable calledWithStates = []

    member _.CalledWithStates = calledWithStates

    interface IComputerEngine with
        member _.GetBestMove(state) =
            async {
                calledWithStates <- state :: calledWithStates
                return bestMoveToReturn
            }

let tests =
    testList "ComputerPlayerWorker Tests" [
        testAsync "Does nothing when it is not the computer's turn" {
            let state = Game.newGame () // White's turn

            let record = {
                GameId = "game1"
                State = state
                WhitePlayerId = "human1"
                BlackPlayerId = "computer"
            }

            let repo = MockGameRepository (Map [ ("game1", record) ])
            let bus = MockMessageBus ()
            let engine = MockComputerEngine (None)
            let logger = NullLogger<ComputerPlayerWorker>.Instance

            let worker = new ComputerPlayerWorker (logger, repo, bus, engine)
            let cancellationTokenSource = new System.Threading.CancellationTokenSource ()

            // Start subscription
            let! _ = Async.AwaitTask (worker.StartAsync (cancellationTokenSource.Token))
            let! handler = waitForSubscription bus

            // Trigger event where White just played
            let! _ = handler "game1"

            // Stop
            let! _ = Async.AwaitTask (worker.StopAsync (cancellationTokenSource.Token))

            // Since WhitePlayerId = "human1" and ActiveColor is White, computer should NOT play
            Expect.isEmpty engine.CalledWithStates "Should not consult the engine on player's turn"
            Expect.isEmpty repo.SavedRecords "Should not write anything to DB"
            Expect.isEmpty bus.PublishedMoves "Should not publish any moves"
        }

        testAsync "Plays a move automatically when it is the computer's turn" {
            let state = Game.newGame () // White's turn
            // Simulate White playing e2 to e4 to make it Black's (computer's) turn
            let whiteMove = {
                From = (4, 1)
                To = (4, 3)
                Promotion = None
            }

            let stateAfterWhite = Game.playMove state whiteMove |> unwrap

            let record = {
                GameId = "game1"
                State = stateAfterWhite // Black's turn (Computer)
                WhitePlayerId = "human1"
                BlackPlayerId = "computer"
            }

            let repo = MockGameRepository (Map [ ("game1", record) ])
            let bus = MockMessageBus ()

            // Computer wants to respond with e7 to e5
            let computerMove =
                Some {
                    From = (4, 6)
                    To = (4, 4)
                    Promotion = None
                }

            let engine = MockComputerEngine (computerMove)
            let logger = NullLogger<ComputerPlayerWorker>.Instance

            let worker = new ComputerPlayerWorker (logger, repo, bus, engine)
            let cancellationTokenSource = new System.Threading.CancellationTokenSource ()

            // Start subscription
            let! _ = Async.AwaitTask (worker.StartAsync (cancellationTokenSource.Token))
            let! handler = waitForSubscription bus

            // Trigger event
            let! _ = handler "game1"

            // Stop
            let! _ = Async.AwaitTask (worker.StopAsync (cancellationTokenSource.Token))

            // Verify the engine was consulted with correct state
            Expect.equal engine.CalledWithStates.Length 1 "Engine should be consulted exactly once"

            Expect.equal
                engine.CalledWithStates.[0]
                stateAfterWhite
                "Consulted state should match state after White move"

            // Verify DB got the computer's move
            Expect.equal repo.SavedRecords.Length 1 "Should save computer's move to DB"
            let savedState = repo.SavedRecords.[0].State
            Expect.equal savedState.ActiveColor White "It should be White's turn again"

            // Verify move published
            Expect.equal bus.PublishedMoves.Length 1 "Should publish the computer's move"
            let publishedGameId, publishedMove = bus.PublishedMoves.[0]
            Expect.equal publishedGameId "game1" "GameId should match"

            let fromPos, toPos = Game.moveFromTo publishedMove
            Expect.equal fromPos (4, 6) "Computer should play e7 (4, 6)"
            Expect.equal toPos (4, 4) "Computer should play e5 (4, 4)"
        }

        testAsync "Handles case where computer has no legal moves (Checkmate or Stalemate)" {
            let state = Game.newGame ()

            let whiteMove = {
                From = (4, 1)
                To = (4, 3)
                Promotion = None
            }

            let stateAfterWhite = Game.playMove state whiteMove |> unwrap

            let record = {
                GameId = "game1"
                State = stateAfterWhite
                WhitePlayerId = "human1"
                BlackPlayerId = "computer"
            }

            let repo = MockGameRepository (Map [ ("game1", record) ])
            let bus = MockMessageBus ()
            let engine = MockComputerEngine (None) // No move returned by engine (checkmate/stalemate/error)
            let logger = NullLogger<ComputerPlayerWorker>.Instance

            let worker = new ComputerPlayerWorker (logger, repo, bus, engine)
            let cancellationTokenSource = new System.Threading.CancellationTokenSource ()

            let! _ = Async.AwaitTask (worker.StartAsync (cancellationTokenSource.Token))
            let! handler = waitForSubscription bus
            let! _ = handler "game1"
            let! _ = Async.AwaitTask (worker.StopAsync (cancellationTokenSource.Token))

            Expect.equal engine.CalledWithStates.Length 1 "Engine consulted once"
            Expect.isEmpty repo.SavedRecords "Should not save any move"
            Expect.isEmpty bus.PublishedMoves "Should not publish any moves"
        }
    ]

[<EntryPoint>]
let main args = runTestsWithCLIArgs [] args tests
