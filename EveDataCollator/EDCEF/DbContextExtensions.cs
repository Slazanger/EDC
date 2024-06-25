using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EveDataCollator.EDCEF
{
    public static class DbContextExtensions
    {
        /// <summary>
        /// Extension method to mimic the AddOrUpdate Method that was available in
        /// earlier EF versions. WARNING: SLOW AF! Currently not in use! Here as a warning!
        /// </summary>
        /// <param name="dbSet"></param>
        /// <param name="entity"></param>
        /// <param name="predicate"></param>
        /// <typeparam name="TEntity"></typeparam>
        public static void AddOrUpdate<TEntity>(this DbSet<TEntity> dbSet, TEntity entity, Func<TEntity, bool> predicate) where TEntity : class
        {
            var context = dbSet.GetService<ICurrentDbContext>().Context;
            var existingEntity = context.Set<TEntity>().Local.FirstOrDefault(predicate);
        
            if (existingEntity == null)
            {
                dbSet.Add(entity);
            }
            else
            {
                dbSet.Update(entity);
            }
        }
    }
}