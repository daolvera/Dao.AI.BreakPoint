using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Requests;

namespace Dao.AI.BreakPoint.Services;

public interface IPlayerService
{
    Task<PlayerDto?> GetByIdAsync(int id);
    Task<IEnumerable<PlayerDto>> GetAllAsync();
    Task<IEnumerable<PlayerDto>> SearchAsync(PlayerSearchRequest playerSearchParameters);
    Task<PlayerWithStatsDto?> GetWithStatsAsync(int id);
    Task<bool> CompleteAsync(CompleteProfileRequest completeProfileRequest, string appUserId);
    Task<int> CreateAsync(CreatePlayerDto createPlayerDto, string? appUserId);
    Task<bool> UpdateAsync(int id, CreatePlayerDto createPlayerDto, string? appUserId);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);
}
