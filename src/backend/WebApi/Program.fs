namespace ChessPlatform.WebApi

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Falco
open Falco.Routing
open ChessPlatform.Application
open ChessPlatform.Infrastructure

module Program =

    type Marker = class end

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)
        
        // Add SignalR
        builder.Services.AddSignalR() |> ignore

        // Wire up Dependencies
        // Tests will override these in WebApplicationFactory
        let pgConn = builder.Configuration.["ConnectionStrings:Postgres"] |> Option.ofObj |> Option.defaultValue ""
        let redisConn = builder.Configuration.["ConnectionStrings:Redis"] |> Option.ofObj |> Option.defaultValue ""
        
        builder.Services.AddSingleton<IGameRepository>(fun _ -> PostgresRepo(pgConn) :> IGameRepository) |> ignore
        builder.Services.AddSingleton<IMessageBus>(fun _ -> RedisBus(redisConn) :> IMessageBus) |> ignore

        let app = builder.Build()

        app.UseRouting() |> ignore
        
        // Extract from DI for compiler-enforced partial application
        let repo = app.Services.GetRequiredService<IGameRepository>()
        let bus = app.Services.GetRequiredService<IMessageBus>()
        
        let falcoEndpoints = [
            post "/api/matchmaking" (Handlers.matchmakingHandler bus)
            post "/api/games/{id}/move" (Handlers.playMoveHandler repo bus)
        ]
        
        app.UseFalco(falcoEndpoints) |> ignore
        
        // Use UseEndpoints for SignalR
        app.UseEndpoints(fun endpoints ->
            endpoints.MapHub<GameHub>("/hubs/game") |> ignore
        ) |> ignore

        app.Run()
        0
