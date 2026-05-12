namespace ChessPlatform.Infrastructure

open ChessPlatform.Domain
open ChessPlatform.Application
open Npgsql
open Npgsql.FSharp

type PostgresRepo(connectionString: string) =

    // Simple schema initialization
    do
        connectionString
        |> Sql.connect
        |> Sql.query
            """
            CREATE TABLE IF NOT EXISTS Games (
                GameId VARCHAR(50) PRIMARY KEY,
                StateFen TEXT NOT NULL,
                WhitePlayerId VARCHAR(50) NOT NULL,
                BlackPlayerId VARCHAR(50) NOT NULL,
                Version INTEGER NOT NULL
            )
            """
        |> Sql.executeNonQuery
        |> ignore

    interface IGameRepository with
        member this.LoadGame(gameId: string) =
            async {
                let result =
                    connectionString
                    |> Sql.connect
                    |> Sql.query "SELECT StateFen, WhitePlayerId, BlackPlayerId, Version FROM Games WHERE GameId = @id"
                    |> Sql.parameters [ "id", Sql.string gameId ]
                    |> Sql.execute (fun read ->
                        {|
                            StateFen = read.string "StateFen"
                            WhitePlayerId = read.string "WhitePlayerId"
                            BlackPlayerId = read.string "BlackPlayerId"
                            Version = read.int "Version"
                        |})

                match result with
                | [] -> return None
                | [ row ] ->
                    match FenParser.parse row.StateFen with
                    | Ok state ->
                        let stateWithVersion = { state with Version = row.Version }

                        return
                            Some
                                {
                                    GameId = gameId
                                    State = stateWithVersion
                                    WhitePlayerId = row.WhitePlayerId
                                    BlackPlayerId = row.BlackPlayerId
                                }
                    | Error _ -> return None // Or throw
                | _ -> return None
            }

        member this.SaveGame(record: GameRecord, expectedVersion: int) =
            async {
                let fen = FenParser.toFen record.State

                try
                    let affected =
                        connectionString
                        |> Sql.connect
                        |> Sql.query
                            "UPDATE Games SET StateFen = @fen, Version = @newVersion WHERE GameId = @id AND Version = @expectedVersion"
                        |> Sql.parameters [
                            "id", Sql.string record.GameId
                            "fen", Sql.string fen
                            "newVersion", Sql.int (expectedVersion + 1)
                            "expectedVersion", Sql.int expectedVersion
                        ]
                        |> Sql.executeNonQuery

                    if affected = 1 then
                        return Ok()
                    else
                        return Error SaveGameError.ConcurrencyConflict
                with ex ->
                    return Error(SaveGameError.PersistenceFailure ex.Message)
            }

        member this.CreateGame(record: GameRecord) =
            async {
                let fen = FenParser.toFen record.State

                try
                    let affected =
                        connectionString
                        |> Sql.connect
                        |> Sql.query
                            "INSERT INTO Games (GameId, StateFen, WhitePlayerId, BlackPlayerId, Version) VALUES (@id, @fen, @w, @b, @v)"
                        |> Sql.parameters [
                            "id", Sql.string record.GameId
                            "fen", Sql.string fen
                            "w", Sql.string record.WhitePlayerId
                            "b", Sql.string record.BlackPlayerId
                            "v", Sql.int record.State.Version
                        ]
                        |> Sql.executeNonQuery

                    if affected = 1 then
                        return Ok()
                    else
                        return Error SaveGameError.ConcurrencyConflict
                with
                | :? PostgresException as ex when ex.SqlState = "23505" -> // unique_violation
                    return Error SaveGameError.ConcurrencyConflict
                | ex -> return Error(SaveGameError.PersistenceFailure ex.Message)
            }
