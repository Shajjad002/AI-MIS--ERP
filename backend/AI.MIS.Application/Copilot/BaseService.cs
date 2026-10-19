using System.Linq.Expressions;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot;

public abstract class BaseService<T> : IBaseService<T> where T : BaseEntity
{
    public virtual Task<T> GetById(long id) => throw Unsupported();
    public virtual Task<IList<T>> GetList() => throw Unsupported();
    public virtual Task<IList<T>> GetList(ISpecification<T> spec) => throw Unsupported();
    public virtual Task<int> Count(ISpecification<T> spec) => throw Unsupported();
    public virtual Task<T> Add(T entity) => throw Unsupported();
    public virtual Task Update(T entity) => throw Unsupported();
    public virtual Task Delete(long id) => throw Unsupported();
    public virtual Task PermanentDelete(T entity) => throw Unsupported();
    public virtual Task<IList<T>> ListAsync() => throw Unsupported();
    public virtual Task<IList<T>> ListAsync(Expression<Func<T, bool>> criteria, params string[] navigations) => throw Unsupported();
    public virtual Task<IList<T>> ListAsync(ISpecification<T> spec) => throw Unsupported();
    public virtual Task UpdateAll(List<T> entity) => throw Unsupported();
    public virtual Task AddAll(List<T> entity) => throw Unsupported();
    public virtual Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> criteria, params string[] navigations) => throw Unsupported();
    public virtual Task<T> FirstOrDefaultAsync(ISpecification<T> spec) => throw Unsupported();
    public virtual Task UpdateAsync(T entity, params Expression<Func<T, object>>[] properties) => throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new($"{typeof(T).Name} does not support generic CRUD operations.");
}
