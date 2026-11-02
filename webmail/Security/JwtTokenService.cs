using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmailHost.Webmail.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EmailHost.Webmail.Security;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(TenantMailbox mailbox)
    {
        var secret = _config["Jwt:SecretKey"] ?? "flitflap_super_secret_jwt_key_that_is_at_least_32_bytes_long!";
        var issuer = _config["Jwt:Issuer"] ?? "flitflap-webmail";
        var audience = _config["Jwt:Audience"] ?? "flitflap-users";
        var expiryHours = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, mailbox.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, mailbox.Address),
            new Claim("mailbox_id", mailbox.Id.ToString()),
            new Claim("address", mailbox.Address),
            new Claim("domain", mailbox.DomainName),
            new Claim("display_name", string.IsNullOrWhiteSpace(mailbox.DisplayName) ? mailbox.LocalPart : mailbox.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

