using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RankingRon.Ports.UseCases;
using RankingRon.UseCases;

namespace RankingRon;

public static class Bootstrapper
{
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        services.AddScoped<IMakelaarUseCase, MakelaarUseCase>();
        return services;
    }
}
