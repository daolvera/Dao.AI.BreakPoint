namespace Dao.AI.BreakPoint.Services.DTOs;

public class UserDto
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required bool IsProfileComplete { get; set; }
    public int PlayerId { get; set; }
}
