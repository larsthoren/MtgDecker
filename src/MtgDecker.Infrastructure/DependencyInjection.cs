using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MtgDecker.Application.Interfaces;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Infrastructure.Data.Repositories;
using MtgDecker.Infrastructure.Parsers;
using MtgDecker.Infrastructure.Scryfall;

namespace MtgDecker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MtgDeckerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IDeckRepository, DeckRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        services.AddHttpClient<IScryfallClient, ScryfallClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.scryfall.com");
            client.DefaultRequestHeaders.Add("User-Agent", "MtgDecker/1.0");
        });
        services.AddScoped<IBulkDataImporter, BulkDataImporter>();

        services.AddSingleton<IDeckParser, MtgoDeckParser>();
        services.AddSingleton<IDeckParser, ArenaDeckParser>();

        return services;
    }
}
