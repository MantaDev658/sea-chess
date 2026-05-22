namespace ChessPlatform.Infrastructure

open ChessPlatform.Domain
open ChessPlatform.Application
open StackExchange.Redis
open System.Text.Json

/// A simple DTO for moves to be published
type MoveDto = {
    From: Position
    To: Position
    Promotion: string option
}

type RedisBus(connectionString: string) =
    let redis = ConnectionMultiplexer.Connect (connectionString)
    let db = redis.GetDatabase ()
    let subscriber = redis.GetSubscriber ()

    let moveToDto (move: Move) : MoveDto =
        let fromPos, toPos = Game.moveFromTo move

        let promo =
            match move with
            | Promotion (_, _, pt) -> Some (pt.ToString ())
            | PromotionCapture (_, _, _, pt) -> Some (pt.ToString ())
            | _ -> None

        {
            From = fromPos
            To = toPos
            Promotion = promo
        }

    interface IMessageBus with
        member this.PublishMovePlayed(gameId: string, move: Move) =
            async {
                let channel = RedisChannel.Literal ($"game:{gameId}:events")
                let dto = moveToDto move
                let json = JsonSerializer.Serialize (dto)
                let! _ = subscriber.PublishAsync (channel, RedisValue json) |> Async.AwaitTask
                return ()
            }

        member this.EnqueueMatchmaking(userId: string, elo: int) =
            async {
                let key = RedisKey "matchmaking:queue"
                let member' = RedisValue userId
                let score = float elo
                let! _ = db.SortedSetAddAsync (key, member', score) |> Async.AwaitTask
                return ()
            }

        member this.SubscribeMovePlayed(handler: string -> Async<unit>) =
            async {
                let channel = RedisChannel.Pattern ("game:*:events")

                let! queue = subscriber.SubscribeAsync (channel) |> Async.AwaitTask

                queue.OnMessage (fun msg ->
                    let ch = msg.Channel.ToString ()
                    // Extract gameId from "game:{gameId}:events"
                    let parts = ch.Split (':')

                    if parts.Length = 3 && parts.[0] = "game" && parts.[2] = "events" then
                        let gameId = parts.[1]
                        Async.Start (handler gameId)
                )

                let disposable =
                    { new System.IDisposable with
                        member _.Dispose() = queue.Unsubscribe ()
                    }

                return disposable
            }
