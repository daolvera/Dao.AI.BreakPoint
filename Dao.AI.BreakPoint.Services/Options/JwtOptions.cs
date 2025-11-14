using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Services.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required]
    public required string Key { get; set; }
    [Required]
    public required string Issuer { get; set; }
    [Required]
    public required string Audience { get; set; }
    [Required]
    [Range(1, 100)]
    public required double RefreshTokenExpiryDays { get; set; }
    [Required]
    [Range(15, 180)]
    public required double AccessTokenExpiryMinutes { get; set; }
}
