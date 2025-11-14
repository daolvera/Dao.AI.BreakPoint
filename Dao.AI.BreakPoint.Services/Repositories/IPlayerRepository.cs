using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Requests;

namespace Dao.AI.BreakPoint.Services.Repositories;

public interface IPlayerRepository
{
    Task<int> AddAsync(Player player, string? appUserId);
    Task<bool> UpdateAsync(Player player, string? appUserId);
    Task<Player?> GetByIdAsync(int id);
    Task<Player?> GetByAppUserIdAsync(string appUserId);
    Task<bool> DeleteItemAsync(int id);
    Task<IEnumerable<Player>> GetValuesAsync(PlayerSearchRequest playerSearchParameters);
    Task<PlayerWithStatsDto?> GetPlayerWithStatsAsync(int id);
}
