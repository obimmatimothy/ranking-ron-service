using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RankingRon.Models;
using RankingRon.Ports.Listing;
using RankingRon.UseCases;
using Xunit;

namespace RankingRon.Tests.UseCases;

public class MakelaarUseCaseTests
{
    private readonly Mock<IListingStore> _listingStoreMock = new();
    private readonly Mock<ILogger<MakelaarUseCase>> _loggerMock = new();
    private readonly MakelaarUseCase _useCase;

    public MakelaarUseCaseTests()
    {
        _useCase = new MakelaarUseCase(_listingStoreMock.Object, _loggerMock.Object);
    }

    [Fact]
    internal async Task GivenNullArgument_WhenGettingTopMakelaars_ArgumentNullExceptionIsThrown()
    {
        Func<Task> act = async () => await _useCase.GetTopMakelaars(null);

        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Theory, AutoData]
    internal async Task GivenListingStoreThrowsException_WhenGettingTopMakelaars_ExceptionIsPropagated(
        SearchCriteria searchCriteria,
        InvalidOperationException exception)
    {
        _listingStoreMock.Setup(x => x.GetAllMakelaars(searchCriteria)).ThrowsAsync(exception);

        Func<Task> act = async () => await _useCase.GetTopMakelaars(searchCriteria);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    [Theory, AutoData]
    internal async Task GivenListingStoreReturnsNull_WhenGettingTopMakelaars_NullIsReturned(SearchCriteria searchCriteria)
    {
        _listingStoreMock.Setup(x => x.GetAllMakelaars(searchCriteria)).ReturnsAsync((IReadOnlyCollection<ListingMakelaar>)null);

        var result = await _useCase.GetTopMakelaars(searchCriteria);

        result.Should().BeNull();
    }

    [Theory, AutoData]
    internal async Task GivenListingStoreReturnsEmptyList_WhenGettingTopMakelaars_EmptyListIsReturned(SearchCriteria searchCriteria)
    {
        _listingStoreMock.Setup(x => x.GetAllMakelaars(searchCriteria)).ReturnsAsync((IReadOnlyCollection<ListingMakelaar>)[]);

        var result = await _useCase.GetTopMakelaars(searchCriteria);

        result.Should().BeEmpty();
    }

    [Theory, AutoData]
    internal async Task GivenListingStoreReturnsMakelaars_WhenGettingTopMakelaars_TopRankedMakelaarsAreReturned(SearchCriteria searchCriteria)
    {
        _listingStoreMock.Setup(x => x.GetAllMakelaars(searchCriteria)).ReturnsAsync(GetMakelaars);

        var result = await _useCase.GetTopMakelaars(searchCriteria);

        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result.Should().BeInDescendingOrder(rm => rm.ListingCount);
        result.First().Position.Should().Be(1);
        result.First().MakelaarId.Should().Be(1);
        result.First().ListingCount.Should().Be(4);
        result.Last().Position.Should().Be(3);
    }

    private static IReadOnlyCollection<ListingMakelaar> GetMakelaars() =>
    [
        new ListingMakelaar{MakelaarId= 1, MakelaarName= "Makelaar 1"},
        new ListingMakelaar{MakelaarId= 2, MakelaarName= "Makelaar 2"},
        new ListingMakelaar{MakelaarId= 3, MakelaarName= "Makelaar 3"},
        new ListingMakelaar{MakelaarId= 1, MakelaarName= "Makelaar 1"},
        new ListingMakelaar{MakelaarId= 2, MakelaarName= "Makelaar 2"},
        new ListingMakelaar{MakelaarId= 1, MakelaarName= "Makelaar 1"},
        new ListingMakelaar{MakelaarId= 3, MakelaarName= "Makelaar 3"},
        new ListingMakelaar{MakelaarId= 1, MakelaarName= "Makelaar 1"},
        new ListingMakelaar{MakelaarId= 3, MakelaarName= "Makelaar 3"},
    ];
}
