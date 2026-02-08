using Microsoft.EntityFrameworkCore;
using MtgDecker.Infrastructure.Data;

namespace MtgDecker.Infrastructure.Tests.Data;

public static class TestDbContextFactory
{
    public static MtgDeckerDbContext Create()
    {
        var options = new DbContextOptionsBuilder<MtgDeckerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new MtgDeckerDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
