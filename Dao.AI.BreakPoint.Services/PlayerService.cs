using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.SearchParams;

namespace Dao.AI.BreakPoint.Services;

public class PlayerService(IPlayerRepository playerRepository) : IPlayerService
{
    public async Task<int> CreateAsync(CreatePlayerDto createPlayerDto, int? appUserId)
    {
        return await playerRepository.AddAsync(createPlayerDto.ToModel(), appUserId);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await playerRepository.DeleteItemAsync(id);
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        var existingPlayersWithMatchingEmails = await playerRepository.GetValuesAsync(new()
        {
            Email = email
        });
        return existingPlayersWithMatchingEmails
             .Any(p => !excludeId.HasValue || p.Id != excludeId.Value);
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return (await playerRepository.GetByIdAsync(id)) is not null;
    }
    // TODO: implement paging
    public async Task<IEnumerable<PlayerDto>> GetAllAsync()
    {
        var allPlayers = await playerRepository.GetValuesAsync(new());
        return allPlayers.Select(PlayerDto.FromModel);
    }

    public async Task<PlayerDto?> GetByIdAsync(int id)
    {
        var player = await playerRepository.GetByIdAsync(id);
        return player is null ?
            null :
            PlayerDto.FromModel(player);
    }

    public async Task<PlayerWithStatsDto?> GetWithStatsAsync(int id)
    {
        return await playerRepository.GetPlayerWithStatsAsync(id);
    }

    public async Task<IEnumerable<PlayerDto>> SearchAsync(PlayerSearchParameters playerSearchParameters)
    {
        return (await playerRepository.GetValuesAsync(playerSearchParameters))
            .Select(PlayerDto.FromModel);
    }

    public async Task<bool> UpdateAsync(int id, CreatePlayerDto createPlayerDto, int? appUserId)
    {
        return await playerRepository.UpdateAsync(createPlayerDto.ToModel(), appUserId);
    }
}
