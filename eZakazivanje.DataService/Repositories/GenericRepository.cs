using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;

namespace eZakazivanje.DataService.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
 {

     public readonly ILogger _logger;
     protected AppDbContext _context;
     internal DbSet<T> _dbSet;

     public GenericRepository(AppDbContext context,ILogger logger)
     {
         _context = context;
         _logger = logger;

         _dbSet = context.Set<T>();
     }

     public virtual async Task<bool> Add(T entity)
     {
         await _dbSet.AddAsync(entity);
         return true;
     }

     public virtual Task<IEnumerable<T>> All()
     {
         throw new NotImplementedException();
     }

     public virtual async Task<bool> Delete(Guid id)
     {
        var entity = await _dbSet.FindAsync(id);
        if (entity == null) return false;
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
         return true;
     }

     public virtual async Task<T?> GetById(Guid id)
     {
         return await _dbSet.FindAsync(id)
;
     }

     public virtual Task<bool> Update(T entity)
     {
         throw new NotImplementedException();
     }
 }
