namespace OrderProcessing.Application.Interfaces.Repositories;

public interface IRepository<T> where T : class
{
    void Add(T entity);
    Task<List<T>> GetAllAsync();
    Task<T?> FindByIdAsync(Guid id);
    Task SaveChangesAsync();
}
