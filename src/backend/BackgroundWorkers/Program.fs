namespace ChessPlatform.BackgroundWorkers

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open ChessPlatform.Application
open ChessPlatform.Infrastructure
open Engine

module Program =

    [<EntryPoint>]
    let main args =
        let builder = Host.CreateApplicationBuilder(args)
        
        let pgConn = builder.Configuration.["ConnectionStrings:Postgres"] |> Option.ofObj |> Option.defaultValue ""
        let redisConn = builder.Configuration.["ConnectionStrings:Redis"] |> Option.ofObj |> Option.defaultValue ""
        
        builder.Services.AddSingleton<IGameRepository>(fun _ -> PostgresRepo(pgConn) :> IGameRepository) |> ignore
        builder.Services.AddSingleton<IMessageBus>(fun _ -> RedisBus(redisConn) :> IMessageBus) |> ignore
        builder.Services.AddSingleton<IComputerEngine>(fun _ -> RandomEngine() :> IComputerEngine) |> ignore
        
        builder.Services.AddHostedService<ComputerPlayerWorker>() |> ignore

        builder.Build().Run()

        0 // exit code