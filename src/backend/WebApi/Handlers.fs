namespace ChessPlatform.WebApi

open Falco
open Falco.Routing
open Falco.Markup
open ChessPlatform.Domain
open ChessPlatform.Application
open Microsoft.AspNetCore.Http

module Handlers =

    type MatchmakingRequest = { Elo: int }

    type MoveRequestDto = {
        FromRow: int
        FromCol: int
        ToRow: int
        ToCol: int
        Promotion: string
    }

    let matchmakingHandler (bus: IMessageBus) : HttpHandler =
        Request.mapJson (fun (req: MatchmakingRequest) ->
            fun ctx ->
                task {
                    let userId =
                        match ctx.Request.Headers.TryGetValue ("X-User-Id") with
                        | true, v -> v.ToString ()
                        | _ -> "anonymous"

                    do! bus.EnqueueMatchmaking (userId, req.Elo)
                    return! Response.withStatusCode 202 >> Response.ofJson {| Message = "Queued" |} <| ctx
                }
        )

    let playMoveHandler (repo: IGameRepository) (bus: IMessageBus) : HttpHandler =
        Request.mapJson (fun (reqDto: MoveRequestDto) ->
            fun ctx ->
                task {
                    let route = Request.getRoute ctx
                    let gameId = route.GetString "id"

                    let userId =
                        match ctx.Request.Headers.TryGetValue ("X-User-Id") with
                        | true, v -> v.ToString ()
                        | _ -> "anonymous"

                    let req: MoveRequest = {
                        From = (reqDto.FromCol, reqDto.FromRow)
                        To = (reqDto.ToCol, reqDto.ToRow)
                        Promotion =
                            match reqDto.Promotion with
                            | "Q" -> Some Queen
                            | "R" -> Some Rook
                            | "B" -> Some Bishop
                            | "N" -> Some Knight
                            | _ -> None
                    }

                    let! result = PlayMoveUseCase.execute repo bus gameId userId req

                    match result with
                    | Ok state ->
                        return!
                            Response.withStatusCode 200 >> Response.ofJson {| Fen = FenParser.toFen state |}
                            <| ctx
                    | Error err ->
                        match err with
                        | PlayMoveUseCase.GameNotFound ->
                            return!
                                Response.withStatusCode 404 >> Response.ofJson {| Error = "Game not found" |}
                                <| ctx
                        | PlayMoveUseCase.NotYourTurn ->
                            return!
                                Response.withStatusCode 400 >> Response.ofJson {| Error = "Not your turn" |}
                                <| ctx
                        | PlayMoveUseCase.DomainError e ->
                            return!
                                Response.withStatusCode 400 >> Response.ofJson {| Error = sprintf "%A" e |}
                                <| ctx
                        | PlayMoveUseCase.ConcurrencyConflict ->
                            return!
                                Response.withStatusCode 409
                                >> Response.ofJson {| Error = "Concurrency conflict" |}
                                <| ctx
                        | PlayMoveUseCase.PersistenceError msg ->
                            return! Response.withStatusCode 500 >> Response.ofJson {| Error = msg |} <| ctx
                }
        )
