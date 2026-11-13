using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace HealthCareManagementSystem.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<EntityEntry<T>> AddAsync(T entity);

        /// <summary>
        /// Loads entities. Use <paramref name="skip"/> / <paramref name="take"/> together with
        /// <paramref name="orderBy"/> for paging so large tables are never loaded completely.
        /// </summary>
        Task<IEnumerable<T>> GetAsync(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<T, object>>[]? includes = null,
            bool tracked = true,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int? skip = null,
            int? take = null);

        Task<T?> GetOneAsync(
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<T, object>>[]? includes = null,
            bool tracked = true);

        Task<bool> AnyAsync(Expression<Func<T, bool>>? filter = null);

        Task<int> CountAsync(Expression<Func<T, bool>>? filter = null);

        void Update(T entity);

        void Delete(T entity);

        /// <summary>
        /// Saves all pending changes. Exceptions (for example <c>DbUpdateException</c>) are not
        /// swallowed, so callers can tell when a save failed.
        /// </summary>
        Task<int> CommitAsync();
    }
}
