namespace RankingRon.Adapters.Funda.Models;

internal sealed class SearchResponse
{
    public PageResponse Paging { get; set; }

    public List<ObjectResponse> Objects { get; set; }
}
