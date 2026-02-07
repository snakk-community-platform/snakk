namespace Snakk.Api.Services;

using Microsoft.IdentityModel.Tokens;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AdminAuthService : IAdminAuthService
{
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public AdminAuthService(
        IAdminUserRepository adminUserRepository,
        IPasswordHasher passwordHasher,
        IConfiguration configuration)
    {
        _adminUserRepository = adminUserRepository;
        _passwordHasher = passwordHasher;
        _secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = configuration["Jwt:Issuer"] ?? "Snakk";
        _audience = configuration["Jwt:Audience"] ?? "Snakk";
        _expirationMinutes = configuration.GetValue<int>("Jwt:Admin:ExpirationMinutes", 480); // 8 hours default for admin
    }

    public async Task<string?> AuthenticateAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var adminUser = await _adminUserRepository.GetByEmailAsync(email);
        if (adminUser == null)
            return null;

        if (!adminUser.IsActive)
            return null;

        if (!_passwordHasher.VerifyPassword(password, adminUser.PasswordHash))
            return null;

        // Update last login time
        adminUser.LastLoginAt = DateTime.UtcNow;
        await _adminUserRepository.UpdateAsync(adminUser);

        // Generate JWT token
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminUser.PublicId),
            new(ClaimTypes.Email, adminUser.Email),
            new(ClaimTypes.Name, adminUser.DisplayName),
            new(ClaimTypes.Role, "Admin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string?> VerifyTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            var roleClaim = principal.FindFirst(ClaimTypes.Role);

            // Verify this is an admin token
            if (roleClaim?.Value != "Admin")
                return null;

            return userIdClaim?.Value;
        }
        catch
        {
            return null;
        }
    }
}
