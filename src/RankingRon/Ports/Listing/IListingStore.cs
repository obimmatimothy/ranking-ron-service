using RankingRon.Exceptions;
using RankingRon.Models;

namespace RankingRon.Ports.Listing;

public interface IListingStore
{
    /// <summary>
    /// Returns makelaars for all listings that match the provided search criteria.
    /// </summary>
    /// <exception cref="UnableToRetrieveDataException"></exception>
    Task<IReadOnlyCollection<ListingMakelaar>> GetAllMakelaars(SearchCriteria searchCriteria);
}
