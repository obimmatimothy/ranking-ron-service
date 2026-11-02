using System;
using System.Collections.Generic;
using System.Text;
using RankingRon.Enums;

namespace RankingRon.Models;

public class SearchCriteria
{
    public ListingType ListingType { get; init; }

    public string City { get; init; }

    public List<string> AdditionalFilters { get; init; }
}
