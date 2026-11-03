using Microsoft.Extensions.Logging;
using RankingRon.Enums;
using RankingRon.Exceptions;
using RankingRon.Models;
using RankingRon.Ports.UseCases;

namespace RankingRon.Application;

internal sealed class Application(IMakelaarUseCase makelaarUseCase, ILogger<Application> logger)
{
    private static readonly Dictionary<string, SearchCriteria> Rankings =
        new()
        {
            {
                "Top 10 makelaars in Amsterdam",
                new SearchCriteria { City = "Amsterdam", ListingType = ListingType.Koop } },
            {
                "Top 10 makelaars in Amsterdam (with tuin)",
                new SearchCriteria { City = "Amsterdam", ListingType = ListingType.Koop, AdditionalFilters = [ "tuin" ] } }
        };

    private readonly IMakelaarUseCase _makelaarUseCase = makelaarUseCase ?? throw new ArgumentNullException(nameof(makelaarUseCase));
    private readonly ILogger<Application> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task Run()
    {
        foreach (var (title, criteria) in Rankings)
        {
            try
            {
                Console.WriteLine(title);
                var topMakelaars = await _makelaarUseCase.GetTopMakelaars(criteria);
                PrintTable(topMakelaars);
            }
            catch (UnableToRetrieveDataException ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving the top makelaars.");
                Console.WriteLine("Unable to retrieve makelaars at this time. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred.");
                Console.WriteLine("An unexpected error occurred. Please try again later.");
            }
        }
    }

    private static void PrintTable(IReadOnlyCollection<RankedMakelaar> makelaars)
    {
        Console.WriteLine();

        if (makelaars.Count == 0)
        {
            Console.WriteLine("No makelaars found.");
            return;
        }

        var nameWidth = Math.Max("Makelaar".Length, makelaars.Max(m => m.MakelaarName?.Length ?? 0));

        Console.WriteLine($"| {"#",-3} | {"Makelaar".PadRight(nameWidth)} | {"Listings",8} |");
        Console.WriteLine($"|{new string('-', 5)}|{new string('-', nameWidth + 2)}|{new string('-', 10)}|");

        foreach (var m in makelaars)
        {
            Console.WriteLine($"| {m.Position,-3} | {(m.MakelaarName ?? "-").PadRight(nameWidth)} | {m.ListingCount,8} |");
        }
    }
}
