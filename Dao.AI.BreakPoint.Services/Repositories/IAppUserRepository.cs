
using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.Repositories;

public interface IAppUserRepository
{
    Task SaveRefreshTokenAsync(string appUserId, string refreshToken, DateTime refreshTokenExpiry);
    Task<AppUser?> GetByRefreshTokenAsync(string refreshToken);
}
