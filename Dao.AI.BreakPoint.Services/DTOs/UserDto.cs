namespace Dao.AI.BreakPoint.Services.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsProfileComplete { get; set; }
    public int PlayerId { get; set; }
}
