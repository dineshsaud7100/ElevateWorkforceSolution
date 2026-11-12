using System.Linq.Expressions;

namespace ElevateWorkforceSolution.Application.Interfaces.Repositories;

/// <summary>
/// Generic data-access abstraction. Keeps EF Core out of the application services while still
/// allowing expressive, server-side composable reads through <see cref="Query"/>.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();

    /// <summary>Change-tracking query, for read-modify-write operations.</summary>
    IQueryable<TEntity> QueryTracked();

    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Total row count, with no predicate.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}

/// <summary>Persists the changes tracked across repositories.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
