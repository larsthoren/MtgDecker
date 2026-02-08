using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MtgDecker.Application.Interfaces;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Infrastructure.Data.Repositories;

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

        return services;
    }
}
