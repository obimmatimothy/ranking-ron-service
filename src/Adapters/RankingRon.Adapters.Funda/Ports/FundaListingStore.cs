using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RankingRon.Adapters.Funda.Configuration;
using RankingRon.Adapters.Funda.Models;
using RankingRon.Exceptions;
using RankingRon.Models;
using RankingRon.Ports.Listing;

namespace RankingRon.Adapters.Funda.Ports;

internal sealed class FundaListingStore(
    HttpClient httpClient,
    ILogger<FundaListingStore> logger,
    FundaSettings fundaSettings) : IListingStore
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILogger<FundaListingStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly FundaSettings _fundaSettings = fundaSettings ?? throw new ArgumentNullException(nameof(fundaSettings));

    public async Task<IReadOnlyCollection<ListingMakelaar>> GetAllMakelaars(SearchCriteria searchCriteria)
    {
        _ = searchCriteria ?? throw new ArgumentNullException(nameof(searchCriteria));

        var page = 1;
        var totalPages = 1;
        var listingMakelaars = new List<ListingMakelaar>();

        try
        {
            var type = searchCriteria.ListingType.ToString().ToLowerInvariant();
            var zo = GetZoekOpdracht(searchCriteria).ToLowerInvariant();

            do
            {
                var searchResponse = await GetSearchResponse(type, zo, page);
                listingMakelaars.AddRange(searchResponse?.Objects?.Select(o => o.ToDomain()) ?? []);

                totalPages = searchResponse?.Paging?.AantalPaginas ?? 0;
                page++;
            } while (page <= totalPages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all listings from Funda API.");
            throw new UnableToRetrieveDataException("Failed to retrieve all listings from Funda API.", ex);
        }

        return listingMakelaars;
    }

    private static string GetZoekOpdracht(SearchCriteria searchCriteria)
    {
        var pathParameters = new List<string>
        {
            searchCriteria.City
        };
        if (searchCriteria.AdditionalFilters is not null)
        {
            pathParameters.AddRange(searchCriteria.AdditionalFilters);
        }

        // It is possible to have a query without any filters or a city
        return pathParameters.All(p => string.IsNullOrWhiteSpace(p))
            ? string.Empty
            : "/" + string.Join("/", pathParameters.Select(Uri.EscapeDataString));
    }

    private string GetPath(string type, string zo, int page) =>
        $"/feeds/Aanbod.svc/json/{_fundaSettings.ApiKey}/?type={type}&zo={zo}&page={page}&pagesize=100";

    private async Task<SearchResponse> GetSearchResponse(string type, string zo, int page)
    {
        var path = GetPath(type, zo, page);
        using var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResponse>();
    }
}
