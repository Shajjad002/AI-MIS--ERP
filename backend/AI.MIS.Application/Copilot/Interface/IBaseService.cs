using System;
using System.Linq.Expressions;
using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Copilot.Interface;

public interface IBaseService<T> where T : BaseEntity
{
    Task<T> GetById(long id);
    Task<IList<T>> GetList();
    Task<IList<T>> GetList(ISpecification<T> spec);
    Task<int> Count(ISpecification<T> spec);
    Task<T> Add(T entity);
    Task Update(T entity);
    Task Delete(long id);
    Task PermanentDelete(T entity);
    Task<IList<T>> ListAsync();
    Task<IList<T>> ListAsync(Expression<Func<T, bool>> criteria, params string[] navigations);
    Task<IList<T>> ListAsync(ISpecification<T> spec);
    Task UpdateAll(List<T> entity);
    Task AddAll(List<T> entity);
    Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> criteria, params string[] navigations);
    Task<T> FirstOrDefaultAsync(ISpecification<T> spec);
    Task UpdateAsync(T entity, params Expression<Func<T, object>>[] properties);
}
