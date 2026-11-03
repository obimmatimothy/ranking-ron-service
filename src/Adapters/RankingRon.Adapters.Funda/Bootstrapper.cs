using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using RankingRon.Adapters.Funda.Configuration;
using RankingRon.Adapters.Funda.Handlers;
using RankingRon.Adapters.Funda.Ports;
using RankingRon.Ports.Listing;

namespace RankingRon.Adapters.Funda;

public static class Bootstrapper
{
    public static IServiceCollection AddFundaAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        var fundaSettings = configuration.GetSection("Funda").Get<FundaSettings>();
        _ = fundaSettings ?? throw new InvalidOperationException("Funda settings are not configured properly in appsettings.json");

        var rateLimiter = CreateRateLimiter(fundaSettings.RateLimiter);
        services.AddSingleton(fundaSettings);
        services.AddSingleton(rateLimiter);
        services.AddTransient<RateLimitingHandler>();

        var httpClientBuilder = services
            .AddHttpClient<IListingStore, FundaListingStore>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(fundaSettings.ApiUrl);
                client.Timeout = TimeSpan.FromMilliseconds(fundaSettings.TimeoutInMilliseconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RankingRon/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                client.DefaultRequestHeaders.Connection.Add("keep-alive");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            });

        httpClientBuilder
            .AddResilienceHandler("funda", (builder, context) => builder.AddRetry(new HttpRetryStrategyOptions
            {
                OnRetry = args =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<FundaListingStore>>();
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying Funda request. Attempt {AttemptNumber} after {RetryDelay}ms. StatusCode: {StatusCode}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode);

                    return ValueTask.CompletedTask;
                },
                MaxRetryAttempts = fundaSettings.RetryCount,
                Delay = TimeSpan.FromMilliseconds(fundaSettings.RetryDelayInMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    HttpClientResiliencePredicates.IsTransient(args.Outcome) ||
                    args.Outcome.Result?.StatusCode == HttpStatusCode.Unauthorized) // Funda api returned 401 when I tried to hit the limit
            }));

        httpClientBuilder.AddHttpMessageHandler<RateLimitingHandler>();

        return services;
    }

    private static SlidingWindowRateLimiter CreateRateLimiter(RateLimiterSettings settings) => new(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = settings.PermitLimit,
        Window = settings.Window,
        SegmentsPerWindow = settings.SegmentsPerWindow,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = settings.QueueLimit
    });
}
