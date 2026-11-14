using System.ComponentModel.DataAnnotations;

namespace MarksApi.Infrastructure;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;
    [Required, MinLength(32)] public string Key { get; set; } = string.Empty;
    [Range(1, 1440)] public int ExpiryMinutes { get; set; } = 60;
}

public sealed class GradingOptions
{
    public const string Section = "Grading";

    [Range(0, 100)] public int PassMark { get; set; } = 50;
}
