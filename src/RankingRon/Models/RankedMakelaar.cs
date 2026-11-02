namespace RankingRon.Models;

public sealed record RankedMakelaar(
    int Position,
    int MakelaarId,
    string MakelaarName,
    int ListingCount);
