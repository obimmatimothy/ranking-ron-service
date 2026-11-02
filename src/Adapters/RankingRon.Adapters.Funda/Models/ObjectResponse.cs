using RankingRon.Models;

namespace RankingRon.Adapters.Funda.Models;

internal sealed class ObjectResponse
{
    public int MakelaarId { get; set; }

    public string MakelaarNaam { get; set; }

    public ListingMakelaar ToDomain() => new()
    {
        MakelaarId = MakelaarId,
        MakelaarName = MakelaarNaam
    };
}
