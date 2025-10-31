using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.SearchParams;

namespace Dao.AI.BreakPoint.Services.Repositories;

public interface IPlayerRepository
{
    Task<int> AddAsync(Player player, int? appUserId);
    Task<bool> UpdateAsync(Player player, int? appUserId);
    Task<Player?> GetByIdAsync(int id);
    Task<bool> DeleteItemAsync(int id);
    Task<IEnumerable<Player>> GetValuesAsync(PlayerSearchParameters playerSearchParameters);
    Task<PlayerWithStatsDto?> GetPlayerWithStatsAsync(int id);
}
