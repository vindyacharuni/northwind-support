namespace SupportDesk.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SupportDesk";

    public string Audience { get; set; } = "SupportDesk";

    public int LifetimeMinutes { get; set; } = 480; // 8 hours (standard working shift)
}
