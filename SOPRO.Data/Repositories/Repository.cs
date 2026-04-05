using Microsoft.EntityFrameworkCore;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SOPRO.Data.Repositories
{
    /// <summary>
    /// Implementación genérica del repositorio usando Entity Framework Core
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly SOPROContext _context;
        protected readonly DbSet<T> _dbSet;
        
        public Repository(SOPROContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }
        
        // ═══════════════════════════════════════════════════════════
        // CONSULTAS
        // ═══════════════════════════════════════════════════════════
        
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }
        
        public virtual async Task<T> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }
        
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }
        
        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }
        
        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
        
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbSet.CountAsync();
            
            return await _dbSet.CountAsync(predicate);
        }
        
        // ═══════════════════════════════════════════════════════════
        // OPERACIONES
        // ═══════════════════════════════════════════════════════════
        
        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            
            await _dbSet.AddAsync(entity);
            return entity;
        }
        
        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            
            await _dbSet.AddRangeAsync(entities);
        }
        
        public virtual Task UpdateAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }
        
        public virtual Task DeleteAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }
        
        public virtual async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                await DeleteAsync(entity);
            }
        }
        
        public virtual Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }
        
        // ═══════════════════════════════════════════════════════════
        // TRANSACCIONES
        // ═══════════════════════════════════════════════════════════
        
        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
