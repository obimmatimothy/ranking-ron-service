using Microsoft.Extensions.Logging;
using RankingRon.Models;
using RankingRon.Ports.Listing;
using RankingRon.Ports.UseCases;

namespace RankingRon.UseCases;

internal sealed class MakelaarUseCase(
    IListingStore listingStore,
    ILogger<MakelaarUseCase> logger) : IMakelaarUseCase
{
    private readonly IListingStore _listingStore = listingStore ?? throw new ArgumentNullException(nameof(listingStore));
    private readonly ILogger<MakelaarUseCase> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IReadOnlyCollection<RankedMakelaar>> GetTopMakelaars(SearchCriteria searchCriteria, int count = 10)
    {
        _ = searchCriteria ?? throw new ArgumentNullException(nameof(searchCriteria));

        _logger.LogInformation("Ranking makelaars for {@SearchCriteria} with top {Count}", searchCriteria, count);

        var makelaars = await _listingStore.GetAllMakelaars(searchCriteria);
        return GetTopRankedMakelaars(makelaars, count);
    }

    private static List<RankedMakelaar> GetTopRankedMakelaars(IEnumerable<ListingMakelaar> makelaars, int count)
    {
        var rankedMakelaars = makelaars?
            .GroupBy(m => m.MakelaarId)
            .Select(g => new RankedMakelaar(
                Position: 0, // Default placeholder, value will be set later
                MakelaarId: g.Key,
                MakelaarName: g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MakelaarName))?.MakelaarName,
                ListingCount: g.Count()))
            .OrderByDescending(rm => rm.ListingCount)
            .ThenBy(rm => rm.MakelaarName) // Ties are broken by alphabetical order of the makelaar name
            .Take(count)
            .ToList();

        for (int i = 0; i < rankedMakelaars?.Count; i++)
        {
            rankedMakelaars[i] = rankedMakelaars[i] with { Position = i + 1 };
        }
        return rankedMakelaars;
    }
}
