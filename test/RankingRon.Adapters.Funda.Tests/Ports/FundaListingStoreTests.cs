using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RankingRon.Adapters.Funda.Configuration;
using RankingRon.Adapters.Funda.Ports;
using RankingRon.Exceptions;
using RankingRon.Models;
using Xunit;

namespace RankingRon.Adapters.Funda.Tests.Ports;

public class FundaListingStoreTests
{
    private readonly Mock<ILogger<FundaListingStore>> _loggerMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly FundaSettings _fundaSettings = new()
    {
        ApiUrl = "https://api.funda.nl/v1/",
        ApiKey = "SuperSecret",
        TimeoutInMilliseconds = 10000,
        RetryCount = 3,
        RetryDelayInMilliseconds = 500
    };
    private readonly FundaListingStore _sut;

    public FundaListingStoreTests()
    {
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri(_fundaSettings.ApiUrl),
            Timeout = TimeSpan.FromMilliseconds(_fundaSettings.TimeoutInMilliseconds)
        };

        _sut = new FundaListingStore(httpClient, _loggerMock.Object, _fundaSettings);
    }

    [Fact]
    internal async Task GivenNullArgument_WhenGettingAllMakelaars_ArgumentNullExceptionIsThrown()
    {
        Func<Task> act = async () => await _sut.GetAllMakelaars(null);
        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Theory, AutoData]
    internal async Task GivenNullDataReturnedFromFunda_WhenGettingAllMakelaars_EmptyListIsReturnedAndCallIsMadeOnce(SearchCriteria searchCriteria)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null")
                };
                return response;
            });
        
        var result = await _sut.GetAllMakelaars(searchCriteria);
        
        result.Should().BeEmpty();
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory, AutoData]
    internal async Task GivenHttpClientThrowsException_WhenGettingMakelaars_UnableToRetrieveDataExceptionIsThrown(SearchCriteria searchCriteria, Exception exception)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        Func<Task> act = async () => await _sut.GetAllMakelaars(searchCriteria);

        await act.Should().ThrowExactlyAsync<UnableToRetrieveDataException>();
    }

    [Theory, AutoData]
    internal async Task GivenHttpClientReturnsNon2xxResponse_WhenGettingMakelaars_UnableToRetrieveDataExceptionIsThrown(SearchCriteria searchCriteria)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        Func<Task> act = async () => await _sut.GetAllMakelaars(searchCriteria);

        await act.Should().ThrowExactlyAsync<UnableToRetrieveDataException>();
    }

    [Theory, AutoData]
    internal async Task GivenFundaHasMultiplePages_WhenGettingMakelaars_AllResultsAreReturned(SearchCriteria searchCriteria)
    {
        var page1Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Objects\":[{\"Id\":1}],\"Paging\":{\"AantalPaginas\":3}}")
        };
        var page2Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Objects\":[{\"Id\":2}],\"Paging\":{\"AantalPaginas\":3}}")
        };
        var page3Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Objects\":[{\"Id\":3}],\"Paging\":{\"AantalPaginas\":3}}")
        };

        _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(page1Response)
            .ReturnsAsync(page2Response)
            .ReturnsAsync(page3Response);

        var result = await _sut.GetAllMakelaars(searchCriteria);

        result.Should().HaveCount(3);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
