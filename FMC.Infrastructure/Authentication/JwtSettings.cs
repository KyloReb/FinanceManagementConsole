namespace FMC.Infrastructure.Authentication;

/// <summary>
/// Configuration settings for JWT generation and validation.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; }
    public int RefreshTokenExpiryDays { get; init; }
}
