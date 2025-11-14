namespace Dao.AI.BreakPoint.Services.Exceptions;

public class EmailAlreadyExistsException() :
    ApplicationException(
            $"User email already exists through a different provider. Please contact support if this is unexpected."
        )
{
}
