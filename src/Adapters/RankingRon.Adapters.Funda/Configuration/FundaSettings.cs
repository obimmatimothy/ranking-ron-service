namespace RankingRon.Adapters.Funda.Configuration;

internal sealed class FundaSettings
{
    public string ApiUrl { get; set; }

    public string ApiKey { get; set; }

    public int TimeoutInMilliseconds { get; set; } = 10000;

    public int RetryCount { get; set; } = 3;

    public int RetryDelayInMilliseconds { get; set; } = 1000;

    public RateLimiterSettings RateLimiter { get; set; } = new RateLimiterSettings();
}
