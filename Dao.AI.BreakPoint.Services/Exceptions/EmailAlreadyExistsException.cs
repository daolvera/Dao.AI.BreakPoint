using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.Exceptions;

public class EmailAlreadyExistsException(OAuthProvider oAuthProvider) :
    ApplicationException(
            $"User email already exists through a different provider. Please sign in with {oAuthProvider} instead. Please contact support if this is unexpected."
        )
{
}
