using Dao.AI.BreakPoint.Services.DTOs;

namespace Dao.AI.BreakPoint.Services.Responses;

public class AuthorizationResponse : RefreshTokenResponse
{
    public UserDto User { get; set; } = null!;
}
