using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.SearchParams;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services.Repositories;

public abstract class BaseRepository<TEntity, TSearchParams>(BreakPointDbContext DbContext)
    where TEntity : BaseModel
    where TSearchParams : SearchParameters
{
    public async Task<int> AddAsync(TEntity entity, int? appUserId)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.CreatedByAppUserId = appUserId;
        UpdateTrackEntity(entity, appUserId);
        DbContext.Set<TEntity>().Add(entity);
        await DbContext.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(TEntity entity, int? appUserId)
    {
        UpdateTrackEntity(entity, appUserId);
        DbContext.Set<TEntity>().Update(entity);
        return (await DbContext.SaveChangesAsync()) > 0;
    }

    private static void UpdateTrackEntity(TEntity entity, int? appUserId)
    {
        if (entity is UpdatableModel updatableEntity)
        {
            updatableEntity.UpdatedAt = DateTime.UtcNow;
            updatableEntity.UpdatedByAppUserId = appUserId;
        }
    }

    public async Task<TEntity?> GetByIdAsync(int id)
    {
        return await DbContext.Set<TEntity>().FindAsync(id);
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        IQueryable<TEntity> itemToDelete = DbContext.Set<TEntity>().Where(e => e.Id == id);
        return (await itemToDelete.ExecuteDeleteAsync()) > 0;
    }

    public async Task<IEnumerable<TEntity>> GetValuesAsync(TSearchParams searchParams)
    {
        IQueryable<TEntity> filteredList = ApplySearchFilters(DbContext.Set<TEntity>(), searchParams);
        return await filteredList.ToListAsync();
    }

    public abstract IQueryable<TEntity> ApplySearchFilters(IQueryable<TEntity> query, TSearchParams searchParams);
}
