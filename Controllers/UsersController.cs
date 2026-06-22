using MapaMensal.Data;
using MapaMensal.Helpers;
using MapaMensal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await db.Users
            .OrderBy(u => u.Username)
            .Select(u => new { u.Id, u.Username, u.Email, u.Role, u.IsActive, u.CreatedAt })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        if (await db.Users.AnyAsync(u => u.Username == dto.Username))
            return BadRequest(new { message = "Nome de utilizador já existe." });

        if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { message = "Email já está em uso." });

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = PasswordHelper.Hash(dto.Password),
            Role = dto.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Email, user.Role, user.IsActive, user.CreatedAt });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (await db.Users.AnyAsync(u => u.Username == dto.Username && u.Id != id))
            return BadRequest(new { message = "Nome de utilizador já existe." });

        user.Username = dto.Username;
        user.Email = dto.Email;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Email, user.Role, user.IsActive });
    }

    [HttpPut("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = PasswordHelper.Hash(dto.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Password redefinida com sucesso." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Role == "Admin" && await db.Users.CountAsync(u => u.Role == "Admin") == 1)
            return BadRequest(new { message = "Não é possível eliminar o único administrador." });
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateUserDto(string Username, string Email, string Password, string Role);
public record UpdateUserDto(string Username, string Email, string Role, bool IsActive);
public record ResetPasswordDto(string NewPassword);
