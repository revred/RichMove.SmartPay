using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RichMove.SmartPay.Core.Time;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RichMove.SmartPay.Api.Security;

/// <summary>
/// JWT token rotation mechanism for enhanced security
/// Automatically refreshes tokens before expiration
/// </summary>
public sealed partial class JwtTokenRotator
{
    private readonly IClock _clock;
    private readonly ILogger<JwtTokenRotator> _logger;
    private readonly JwtTokenOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly object _lockObject = new();
    private string? _currentToken;
    private DateTime _tokenExpiryUtc;

    public JwtTokenRotator(IClock clock, ILogger<JwtTokenRotator> logger, IOptions<JwtTokenOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _logger = logger;
        _options = options.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Get current valid token, rotating if necessary
    /// </summary>
    public string GetValidToken(string clientId, string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(scopes);

        if (ShouldRotateToken())
        {
            lock (_lockObject)
            {
                if (ShouldRotateToken())
                {
                    RotateToken(clientId, scopes);
                }
            }
        }

        return _currentToken ?? throw new InvalidOperationException("No valid token available");
    }

    /// <summary>
    /// Force token rotation (for testing or security events)
    /// </summary>
    public void ForceRotation(string clientId, string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(scopes);

        lock (_lockObject)
        {
            Log.TokenRotationForced(_logger, clientId);
            RotateToken(clientId, scopes);
        }
    }

    private bool ShouldRotateToken()
    {
        if (_currentToken == null) return true;

        var rotationThreshold = _tokenExpiryUtc.AddMinutes(-_options.RotationThresholdMinutes);
        return _clock.UtcNow >= rotationThreshold;
    }

    private void RotateToken(string clientId, string[] scopes)
    {
        var now = _clock.UtcNow;
        var expiry = now.AddMinutes(_options.TokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, new DateTimeOffset(expiry).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("scope", string.Join(" ", scopes))
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);

        _currentToken = tokenString;
        _tokenExpiryUtc = expiry;

        Log.TokenRotated(_logger, clientId, expiry);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "JWT token rotated for client {ClientId}, expires at {ExpiryTime}")]
        public static partial void TokenRotated(ILogger logger, string clientId, DateTime expiryTime);

        [LoggerMessage(EventId = 5102, Level = LogLevel.Warning, Message = "JWT token rotation forced for client {ClientId}")]
        public static partial void TokenRotationForced(ILogger logger, string clientId);
    }
}

/// <summary>
/// JWT token configuration options
/// </summary>
public sealed class JwtTokenOptions
{
    public SymmetricSecurityKey SigningKey { get; set; } = null!;
    public int TokenLifetimeMinutes { get; set; } = 60;
    public int RotationThresholdMinutes { get; set; } = 5;
}