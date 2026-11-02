using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RankingRon;
using RankingRon.Adapters.Funda;
using RankingRon.Application;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddDomain();
        services.AddFundaAdapter(context.Configuration);

        services.AddTransient<Application>();
    })
    .Build();

await host.Services.GetRequiredService<Application>().Run();

