namespace Dao.AI.BreakPoint.Services;

public class DummyAuthenticationService : IAuthenticationService
{
    // TODO implement connecting oauth account to an app user and the whole flow
    public async Task<int?> GetAppUserId()
    {
        await Task.CompletedTask;
        return null;
    }
}
