using System.Linq.Expressions;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ElevateWorkforceSolution.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IRepository{TEntity}"/>.</summary>
public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public EfRepository(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public IQueryable<TEntity> Query() => DbSet.AsNoTracking();

    public IQueryable<TEntity> QueryTracked() => DbSet;

    public Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default) =>
        DbSet.FindAsync(new object?[] { id }, cancellationToken).AsTask();

    public Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().CountAsync(predicate, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().CountAsync(cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => DbSet.Update(entity);

    public void Remove(TEntity entity) => DbSet.Remove(entity);
}

/// <summary>Tracks entities and persists them once per logical operation.</summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public EfUnitOfWork(ApplicationDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
