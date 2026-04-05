using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SOPRO.Data.Repositories
{
    /// <summary>
    /// Interfaz genérica de repositorio con operaciones CRUD estándar
    /// </summary>
    public interface IRepository<T> where T : class
    {
        // ═══════════════════════════════════════════════════════════
        // CONSULTAS
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Obtiene todos los registros
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();
        
        /// <summary>
        /// Obtiene un registro por ID
        /// </summary>
        Task<T> GetByIdAsync(int id);
        
        /// <summary>
        /// Busca registros que cumplan con una condición
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        
        /// <summary>
        /// Busca el primer registro que cumpla con una condición
        /// </summary>
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        
        /// <summary>
        /// Verifica si existe algún registro que cumpla con una condición
        /// </summary>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        
        /// <summary>
        /// Cuenta registros que cumplan con una condición
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);
        
        // ═══════════════════════════════════════════════════════════
        // OPERACIONES
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Agrega un nuevo registro
        /// </summary>
        Task<T> AddAsync(T entity);
        
        /// <summary>
        /// Agrega múltiples registros
        /// </summary>
        Task AddRangeAsync(IEnumerable<T> entities);
        
        /// <summary>
        /// Actualiza un registro existente
        /// </summary>
        Task UpdateAsync(T entity);
        
        /// <summary>
        /// Elimina un registro
        /// </summary>
        Task DeleteAsync(T entity);
        
        /// <summary>
        /// Elimina un registro por ID
        /// </summary>
        Task DeleteAsync(int id);
        
        /// <summary>
        /// Elimina múltiples registros
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<T> entities);
        
        // ═══════════════════════════════════════════════════════════
        // TRANSACCIONES
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Guarda los cambios en la base de datos
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
