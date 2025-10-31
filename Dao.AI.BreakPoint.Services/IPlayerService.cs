using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.SearchParams;

namespace Dao.AI.BreakPoint.Services;

public interface IPlayerService
{
    Task<PlayerDto?> GetByIdAsync(int id);
    Task<IEnumerable<PlayerDto>> GetAllAsync();
    Task<IEnumerable<PlayerDto>> SearchAsync(PlayerSearchParameters playerSearchParameters);
    Task<PlayerWithStatsDto?> GetWithStatsAsync(int id);
    Task<int> CreateAsync(CreatePlayerDto createPlayerDto, int? appUserId);
    Task<bool> UpdateAsync(int id, CreatePlayerDto createPlayerDto, int? appUserId);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);
}
