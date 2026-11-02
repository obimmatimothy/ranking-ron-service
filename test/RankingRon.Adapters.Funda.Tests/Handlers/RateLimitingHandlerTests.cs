using System.Net;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RankingRon.Adapters.Funda.Handlers;
using Xunit;

namespace RankingRon.Adapters.Funda.Tests.Handlers;

public class RateLimitingHandlerTests
{
    private const string RequestUri = "https://someurl.com";

    private readonly Mock<ILogger<RateLimitingHandler>> _loggerMock = new();
    private readonly Mock<HttpMessageHandler> _innerHandlerMock = new();
    private readonly SlidingWindowRateLimiter _rateLimiter = new(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 1,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 1,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = false
    });
    private readonly HttpMessageInvoker _sut;

    public RateLimitingHandlerTests()
    {
        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new RateLimitingHandler(_rateLimiter, _loggerMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };

        _sut = new HttpMessageInvoker(handler);
    }

    [Fact]
    public async Task GivenPermitIsAvailable_WhenInvokingHandler_ShouldForwardRequestToInnerHandler_()
    {
        var response = await _sut.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri), default);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GivenPermitsAreExhausted_WhenInvokingHandler_ShouldReturnTooManyRequestsWithoutCallingInnerHandler()
    {
        await _sut.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri), default);

        var response = await _sut.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        _loggerMock.Verify(
           l => l.Log(
               LogLevel.Warning,
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Returning HTTP 429")),
               It.IsAny<Exception>(),
               It.IsAny<Func<It.IsAnyType, Exception, string>>()),
           Times.Once);
    }
}
