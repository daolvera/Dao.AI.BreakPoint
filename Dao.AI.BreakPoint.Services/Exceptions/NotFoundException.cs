namespace Dao.AI.BreakPoint.Services.Exceptions;

public class NotFoundException(string displayName) : ApplicationException($"The entity {displayName} could not be found")
{
}
