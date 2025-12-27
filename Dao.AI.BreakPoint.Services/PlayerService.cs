using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.Requests;

namespace Dao.AI.BreakPoint.Services;

public class PlayerService(IPlayerRepository playerRepository) : IPlayerService
{
    public async Task<int> CreateAsync(CreatePlayerDto createPlayerDto, string? appUserId)
    {
        return await playerRepository.AddAsync(createPlayerDto.ToModel(), appUserId);
    }

    public async Task<bool> CompleteAsync(
        CompleteProfileRequest completeProfileRequest,
        string appUserId
    )
    {
        Player player =
            await playerRepository.GetByAppUserIdAsync(appUserId)
            ?? throw new NotFoundException($"Player with App User Id {appUserId}");
        player.UstaRating = completeProfileRequest.UstaRating;
        player.Name = completeProfileRequest.Name;
        player.Handedness = completeProfileRequest.Handedness;
        player.AppUser!.IsProfileComplete = true;
        await playerRepository.UpdateAsync(player, appUserId);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await playerRepository.DeleteItemAsync(id);
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        var existingPlayersWithMatchingEmails = await playerRepository.GetValuesAsync(
            new() { Email = email }
        );
        return existingPlayersWithMatchingEmails.Any(p =>
            !excludeId.HasValue || p.Id != excludeId.Value
        );
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return (await playerRepository.GetByIdAsync(id)) is not null;
    }

    // TODO DAO: implement paging
    public async Task<IEnumerable<PlayerDto>> GetAllAsync()
    {
        var allPlayers = await playerRepository.GetValuesAsync(new());
        return allPlayers.Select(PlayerDto.FromModel);
    }

    public async Task<PlayerDto?> GetByIdAsync(int id)
    {
        var player = await playerRepository.GetByIdAsync(id);
        return player is null ? null : PlayerDto.FromModel(player);
    }

    public async Task<PlayerDto?> GetByAppUserIdAsync(string appUserId)
    {
        var player = await playerRepository.GetByAppUserIdAsync(appUserId);
        return player is null ? null : PlayerDto.FromModel(player);
    }

    public async Task<PlayerWithStatsDto?> GetWithStatsAsync(int id)
    {
        return await playerRepository.GetPlayerWithStatsAsync(id);
    }

    public async Task<IEnumerable<PlayerDto>> SearchAsync(
        PlayerSearchRequest playerSearchParameters
    )
    {
        return (await playerRepository.GetValuesAsync(playerSearchParameters)).Select(
            PlayerDto.FromModel
        );
    }

    public async Task<bool> UpdateAsync(int id, CreatePlayerDto createPlayerDto, string? appUserId)
    {
        return await playerRepository.UpdateAsync(createPlayerDto.ToModel(), appUserId);
    }
}
