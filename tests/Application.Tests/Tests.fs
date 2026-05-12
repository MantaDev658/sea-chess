module Tests

open Expecto
open ChessPlatform.Domain
open ChessPlatform.Application

// Mocks
type MockRepo (initialState: GameRecord option, saveResult: Result<unit, SaveGameError>) =
    interface IGameRepository with
        member _.LoadGame _ = async { return initialState }
        member _.SaveGame (_, _) = async { return saveResult }
        member _.CreateGame _ = async { return Ok () }

type MockBus () =
    let mutable publishedMoves = []
    member _.PublishedMoves = publishedMoves
    interface IMessageBus with
        member _.PublishMovePlayed (_, move) = async {
            publishedMoves <- move :: publishedMoves
        }
        member _.EnqueueMatchmaking (_, _) = async { () }

let tests =
    testList "PlayMoveUseCase Tests" [
        testAsync "Returns GameNotFound when game does not exist" {
            let repo = MockRepo(None, Ok ())
            let bus = MockBus()
            let req = { From = (0,0); To = (0,1); Promotion = None }
            
            let! result = PlayMoveUseCase.execute repo bus "game1" "user1" req
            Expect.equal result (Error PlayMoveUseCase.GameNotFound) "Should be GameNotFound"
        }

        testAsync "Returns NotYourTurn when user is not the active player" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Ok ())
            let bus = MockBus()
            let req = { From = (4,1); To = (4,3); Promotion = None }
            
            // It's White's turn, but user is Black
            let! result = PlayMoveUseCase.execute repo bus "game1" "b1" req
            Expect.equal result (Error PlayMoveUseCase.NotYourTurn) "Should be NotYourTurn"
        }

        testAsync "Returns DomainError on invalid move" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Ok ())
            let bus = MockBus()
            
            // Invalid move (pawn jumping over piece etc, or just impossible move)
            let req = { From = (4,1); To = (4,5); Promotion = None }
            
            let! result = PlayMoveUseCase.execute repo bus "game1" "w1" req
            match result with
            | Error (PlayMoveUseCase.DomainError _) -> ()
            | _ -> failwith "Expected DomainError"
        }

        testAsync "Returns ConcurrencyConflict on OCC failure" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Error SaveGameError.ConcurrencyConflict)
            let bus = MockBus()
            let req = { From = (4,1); To = (4,3); Promotion = None }
            
            let! result = PlayMoveUseCase.execute repo bus "game1" "w1" req
            Expect.equal result (Error PlayMoveUseCase.ConcurrencyConflict) "Should be ConcurrencyConflict"
            Expect.isEmpty bus.PublishedMoves "Should not publish move on conflict"
        }

        testAsync "Succeeds and publishes event on valid move" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Ok ())
            let bus = MockBus()
            let req = { From = (4,1); To = (4,3); Promotion = None }
            
            let! result = PlayMoveUseCase.execute repo bus "game1" "w1" req
            
            match result with
            | Ok newState ->
                Expect.equal bus.PublishedMoves.Length 1 "Should publish exactly one event"
            | Error e -> failwithf "Expected success but got %A" e
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
