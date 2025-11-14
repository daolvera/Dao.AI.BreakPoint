using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services.Repositories;

public class AppUserRepository(BreakPointDbContext DbContext) : IAppUserRepository
{
    public async Task SaveRefreshTokenAsync(string appUserId, string refreshToken, DateTime refreshTokenExpiry)
    {
        var user = await DbContext.Users.FindAsync(appUserId) ??
            throw new NotFoundException($"App user {appUserId}");
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = refreshTokenExpiry;
        await DbContext.SaveChangesAsync();
    }

    public async Task<AppUser?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await DbContext.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
    }
}
