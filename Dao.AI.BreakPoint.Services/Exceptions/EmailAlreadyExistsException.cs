namespace Dao.AI.BreakPoint.Services.Exceptions;

public class EmailAlreadyExistsException(string oAuthProvider) :
    ApplicationException(
            $"User email already exists through a different provider. Please sign in with {oAuthProvider} instead. Please contact support if this is unexpected."
        )
{
}
