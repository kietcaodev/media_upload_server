using Microsoft.AspNetCore.Mvc;
using MediaUpload.Application.DTOs;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Interfaces;
using MediaUpload.Domain.Enums;
using MediaUpload.API.Middleware;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/credentials")]
[RequirePermission("config")]
public class CredentialsController(IApiCredentialRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await repo.GetAllAsync()).Select(MapDto));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await repo.GetByIdAsync(id);
        return c == null ? NotFound() : Ok(MapDto(c));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCredentialRequest req)
    {
        string rawToken;
        string hashedSecret;
        string? username = null;

        if (req.AuthType == AuthType.Basic)
        {
            rawToken = GenerateToken(32);
            username = req.Username ?? throw new ArgumentException("Username required for Basic auth");
            hashedSecret = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 12);
        }
        else
        {
            rawToken = GenerateToken(48);
            hashedSecret = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 12);
        }

        var cred = new ApiCredential
        {
            Name         = req.Name,
            AuthType     = req.AuthType,
            HashedSecret = hashedSecret,
            TokenPrefix  = rawToken[..Math.Min(8, rawToken.Length)],
            Username     = username,
            CanUpload    = req.CanUpload,
            CanReadJobs  = req.CanReadJobs,
            CanConfig    = req.CanConfig,
            AllowedErp   = req.AllowedErp,
        };

        await repo.AddAsync(cred);

        return CreatedAtAction(nameof(Get), new { id = cred.Id }, new CreateCredentialResponse(
            cred.Id, cred.Name, cred.AuthType.ToString(), rawToken, username, cred.TokenPrefix));
    }

    [HttpPost("{id:int}/rotate")]
    public async Task<IActionResult> Rotate(int id)
    {
        var cred = await repo.GetByIdAsync(id);
        if (cred == null) return NotFound();

        var rawToken = GenerateToken(48);
        cred.HashedSecret = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 12);
        cred.TokenPrefix  = rawToken[..Math.Min(8, rawToken.Length)];
        await repo.UpdateAsync(cred);

        return Ok(new { rawToken, tokenPrefix = cred.TokenPrefix, message = "Save this token – it won't be shown again." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCredentialRequest req)
    {
        var cred = await repo.GetByIdAsync(id);
        if (cred == null) return NotFound();
        cred.Name       = req.Name;
        cred.CanUpload  = req.CanUpload;
        cred.CanReadJobs= req.CanReadJobs;
        cred.CanConfig  = req.CanConfig;
        cred.AllowedErp = req.AllowedErp;
        cred.Enabled    = req.Enabled;
        await repo.UpdateAsync(cred);
        return Ok(MapDto(cred));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id);
        return NoContent();
    }

    private static string GenerateToken(int bytes) =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(bytes))
               .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private static CredentialDto MapDto(ApiCredential c) => new(
        c.Id, c.Name, c.AuthType.ToString(), c.TokenPrefix, c.Username,
        c.CanUpload, c.CanReadJobs, c.CanConfig, c.AllowedErp,
        c.Enabled, c.CreatedAtUtc.ToString("O"), c.LastUsedAtUtc?.ToString("O"));
}

public record UpdateCredentialRequest(
    string Name,
    bool CanUpload,
    bool CanReadJobs,
    bool CanConfig,
    string AllowedErp,
    bool Enabled
);
