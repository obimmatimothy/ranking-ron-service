namespace RankingRon.Adapters.Funda.Configuration;

internal sealed class RateLimiterSettings
{
    public int PermitLimit { get; set; } = 99;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    public int SegmentsPerWindow { get; set; } = 6;

    public int QueueLimit { get; set; } = 50;
}
