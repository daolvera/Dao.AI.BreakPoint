
namespace Dao.AI.BreakPoint.Services;

public interface IAuthenticationService
{
    Task<int?> GetAppUserId();
}