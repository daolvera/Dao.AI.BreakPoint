namespace Dao.AI.BreakPoint.Services.DTOs;

public class UserDto
{
    public required string Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public required bool IsProfileComplete { get; set; }
    public required int PlayerId { get; set; }
}
