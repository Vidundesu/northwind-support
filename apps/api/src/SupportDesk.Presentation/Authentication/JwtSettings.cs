namespace SupportDesk.Presentation.Authentication;

/// <summary>JWT signing and validation settings supplied by application configuration.</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 60;

    public bool IsValid
    {
        get
        {
            try
            {
                Validate();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Authentication:Jwt:Issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Authentication:Jwt:Audience must be configured.");
        }

        if (EncodingByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        if (LifetimeMinutes is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:LifetimeMinutes must be between 1 and 120.");
        }
    }

    private static int EncodingByteCount(string value) =>
        System.Text.Encoding.UTF8.GetByteCount(value ?? string.Empty);
}
