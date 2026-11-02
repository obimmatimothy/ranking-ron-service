using RankingRon.Exceptions;
using RankingRon.Models;

namespace RankingRon.Ports.UseCases;

public interface IMakelaarUseCase
{
    /// <summary>
    /// Based on the provided search criteria, returns a ranked list of makelaars with their respective listing counts
    /// </summary>
    /// <param name="searchCriteria">The criteria to filter listings</param>
    /// <param name="count">The top number of makelaars to return. Defaults to 10 if not provided</param>
    /// <exception cref="UnableToRetrieveDataException"></exception>
    Task<IReadOnlyCollection<RankedMakelaar>> GetTopMakelaars(SearchCriteria searchCriteria, int count = 10);
}
