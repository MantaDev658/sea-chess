module Tests

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open Expecto
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open ChessPlatform.Domain
open ChessPlatform.Application
open ChessPlatform.WebApi

type MockRepo (initialState: GameRecord option, saveResult: Result<unit, SaveGameError>) =
    interface IGameRepository with
        member _.LoadGame _ = async { return initialState }
        member _.SaveGame (_, _) = async { return saveResult }
        member _.CreateGame _ = async { return Ok () }

type MockBus () =
    interface IMessageBus with
        member _.PublishMovePlayed (_, _) = async { () }
        member _.EnqueueMatchmaking (_, _) = async { () }

type AppFactory(repo: IGameRepository) =
    inherit WebApplicationFactory<Program.Marker>()
    override this.ConfigureWebHost(builder) =
        builder.UseSolutionRelativeContentRoot("src/backend/WebApi") |> ignore
        builder.ConfigureServices(fun services ->
            services.AddSingleton<IGameRepository>(fun _ -> repo) |> ignore
            services.AddSingleton<IMessageBus>(fun _ -> MockBus() :> IMessageBus) |> ignore
        ) |> ignore

let tests =
    testList "WebApi HTTP E2E Tests" [
        
        testTask "POST /api/games/{id}/move - 200 OK on valid move" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Ok ())
            let factory = new AppFactory(repo)
            let client = factory.CreateClient()
            
            client.DefaultRequestHeaders.Add("X-User-Id", "w1")
            
            let json = """{"FromRow": 1, "FromCol": 4, "ToRow": 3, "ToCol": 4, "Promotion": ""}"""
            let content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            let! response : HttpResponseMessage = client.PostAsync("/api/games/game1/move", content)
            let! body = response.Content.ReadAsStringAsync()
            Expect.equal response.StatusCode HttpStatusCode.OK (sprintf "Should return 200 OK. Body: %s" body)
        }

        testTask "POST /api/games/{id}/move - 404 Not Found if game missing" {
            let repo = MockRepo(None, Ok ())
            let factory = new AppFactory(repo)
            let client = factory.CreateClient()
            client.DefaultRequestHeaders.Add("X-User-Id", "w1")
            
            let json = """{"FromRow": 1, "FromCol": 4, "ToRow": 3, "ToCol": 4, "Promotion": ""}"""
            let content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            let! response : HttpResponseMessage = client.PostAsync("/api/games/game1/move", content)
            
            Expect.equal response.StatusCode HttpStatusCode.NotFound "Should return 404 Not Found"
        }

        testTask "POST /api/games/{id}/move - 400 Bad Request on invalid domain move" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Ok ())
            let factory = new AppFactory(repo)
            let client = factory.CreateClient()
            client.DefaultRequestHeaders.Add("X-User-Id", "w1")
            
            // Invalid move: e2 to e5 (illegal for pawn)
            let json = """{"FromRow": 1, "FromCol": 4, "ToRow": 4, "ToCol": 4, "Promotion": ""}"""
            let content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            let! response : HttpResponseMessage = client.PostAsync("/api/games/game1/move", content)
            
            Expect.equal response.StatusCode HttpStatusCode.BadRequest "Should return 400 Bad Request"
        }

        testTask "POST /api/games/{id}/move - 409 Conflict on OCC failure" {
            let state = Game.newGame()
            let record = { GameId = "game1"; State = state; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
            let repo = MockRepo(Some record, Error SaveGameError.ConcurrencyConflict)
            let factory = new AppFactory(repo)
            let client = factory.CreateClient()
            client.DefaultRequestHeaders.Add("X-User-Id", "w1")
            
            let json = """{"FromRow": 1, "FromCol": 4, "ToRow": 3, "ToCol": 4, "Promotion": ""}"""
            let content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            let! response : HttpResponseMessage = client.PostAsync("/api/games/game1/move", content)
            let! body = response.Content.ReadAsStringAsync()
            Expect.equal response.StatusCode HttpStatusCode.Conflict (sprintf "Should return 409 Conflict. Body: %s" body)
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
