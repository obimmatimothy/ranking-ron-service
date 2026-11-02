using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace RankingRon.Adapters.Funda.Handlers;

internal sealed class RateLimitingHandler(
    SlidingWindowRateLimiter rateLimiter,
    ILogger<RateLimitingHandler> logger) : DelegatingHandler
{
    private readonly SlidingWindowRateLimiter _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    private readonly ILogger<RateLimitingHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);
        if (lease.IsAcquired)
        {
            return await base.SendAsync(request, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Unexpected heavy load detected. Returning HTTP 429");
            return new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
        }
    }
}
