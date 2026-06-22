using MapaMensal.Data;
using MapaMensal.Helpers;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

        if (user is null || !PasswordHelper.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Utilizador ou password incorretos." });

        var token = GenerateToken(user);
        return Ok(new
        {
            token,
            user = new { user.Id, user.Username, user.Email, user.Role }
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return Ok(new { user.Id, user.Username, user.Email, user.Role });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!PasswordHelper.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Password atual incorreta." });

        user.PasswordHash = PasswordHelper.Hash(dto.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Password alterada com sucesso." });
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginDto(string Username, string Password);
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
