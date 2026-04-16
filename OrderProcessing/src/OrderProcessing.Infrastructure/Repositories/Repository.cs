using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Interfaces.Repositories;
using OrderProcessing.Infrastructure.Data;

namespace OrderProcessing.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;

    public Repository(AppDbContext db) => Db = db;

    public void Add(T entity) => Db.Set<T>().Add(entity);

    public Task<List<T>> GetAllAsync() => Db.Set<T>().ToListAsync();

    public async Task<T?> FindByIdAsync(Guid id) => await Db.Set<T>().FindAsync(id);

    public Task SaveChangesAsync() => Db.SaveChangesAsync();
}
