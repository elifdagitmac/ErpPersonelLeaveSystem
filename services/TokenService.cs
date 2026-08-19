using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ErpPersonelLeaveSystem.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private (SymmetricSecurityKey key, string issuer, string audience, int expiresMinutes) GetJwtSettings()
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;
        var expiresMinutes = int.Parse(jwtSection["ExpiresMinutes"] ?? "480");
        return (key, issuer, audience, expiresMinutes);
    }

    private string CreateToken(IEnumerable<Claim> claims)
    {
        var (key, issuer, audience, expiresMinutes) = GetJwtSettings();
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateEmployeeToken(int companyId, int employeeId, string name, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employeeId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("CompanyId", companyId.ToString())
        };

        return CreateToken(claims);
    }

    public string GenerateSuperAdminToken(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "0"),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, "SuperAdmin")
        };

        return CreateToken(claims);
    }
}
