#nowarn "0044"
module Tests

open Expecto
open Testcontainers.PostgreSql
open Testcontainers.Redis
open System.Threading.Tasks
open ChessPlatform.Domain
open ChessPlatform.Application
open ChessPlatform.Infrastructure

let unwrap = function
    | Ok x -> x
    | Error e -> failwithf "Expected Ok, got Error: %A" e

let expectError = function
    | Ok _ -> failwith "Expected Error, got Ok"
    | Error e -> e

// Global fixture
let mutable pgContainer : PostgreSqlContainer = null
let mutable redisContainer : RedisContainer = null

let setup () =
    task {
        pgContainer <- PostgreSqlBuilder().WithImage("postgres:16-alpine").Build()
        redisContainer <- RedisBuilder().WithImage("redis:7-alpine").Build()
        do! pgContainer.StartAsync()
        do! redisContainer.StartAsync()
    } |> Async.AwaitTask |> Async.RunSynchronously

let teardown () =
    task {
        do! pgContainer.DisposeAsync().AsTask()
        do! redisContainer.DisposeAsync().AsTask()
    } |> Async.AwaitTask |> Async.RunSynchronously

let tests () =
    let pgConnectionString = pgContainer.GetConnectionString()
    let redisConnectionString = redisContainer.GetConnectionString()

    let repo = PostgresRepo(pgConnectionString) :> IGameRepository
    let bus = RedisBus(redisConnectionString) :> IMessageBus

    testList "Infrastructure Tests" [
        testAsync "PostgresRepo OCC" {
                let initialState = Game.newGame ()
                let gameId = "test-game-1"
                let record = { GameId = gameId; State = initialState; WhitePlayerId = "w1"; BlackPlayerId = "b1" }
                
                // 1. Create Game
                let! res1 = repo.CreateGame(record)
                Expect.isTrue (Result.isOk res1) "create should succeed"

                // 2. Play a move
                let req : MoveRequest = { From = (4,1); To = (4,3); Promotion = None }
                let newState = Game.playMove initialState req |> unwrap
                let newRecord = { record with State = newState }

                // 3. Save game with correct version (0)
                let! res2 = repo.SaveGame(newRecord, 0)
                Expect.isTrue (Result.isOk res2) "save with correct version should succeed"

                // 4. Try saving again with the old version (0) -> should fail
                let! res3 = repo.SaveGame(newRecord, 0)
                Expect.equal (expectError res3) SaveGameError.ConcurrencyConflict "save with old version should fail with ConcurrencyConflict"

                // 5. Save with new version (1) -> should succeed
                let! res4 = repo.SaveGame(newRecord, 1)
                Expect.isTrue (Result.isOk res4) "save with correct updated version should succeed"
        }

        testAsync "RedisBus Pub/Sub" {
                let mux = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString)
                let sub = mux.GetSubscriber()
                let channel = StackExchange.Redis.RedisChannel.Literal("game:test-game-2:events")
                
                let mutable messageReceived = false
                sub.Subscribe(channel, fun _ _ -> messageReceived <- true) |> ignore

                // Publish
                let move = Normal((4,1), (4,3))
                do! bus.PublishMovePlayed("test-game-2", move)

                // Wait a bit for message to propagate
                do! Async.Sleep 500

                Expect.isTrue messageReceived "message should be received"
        }
    ]

[<EntryPoint>]
let main args =
    setup()
    let res = runTestsWithCLIArgs [] args (tests())
    teardown()
    res
