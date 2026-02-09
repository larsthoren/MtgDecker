using MediatR;
using MtgDecker.Infrastructure.Data;

namespace MtgDecker.Infrastructure.Behaviors;

/// <summary>
/// Clears the EF Core change tracker before each MediatR request.
/// In Blazor Server, DbContext is scoped per-circuit (long-lived),
/// so stale tracked entities from previous requests cause concurrency errors.
/// </summary>
public class DbContextResetBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly MtgDeckerDbContext _context;

    public DbContextResetBehavior(MtgDeckerDbContext context)
    {
        _context = context;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();
        return await next(cancellationToken);
    }
}
