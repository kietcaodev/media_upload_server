using Microsoft.AspNetCore.Mvc;
using MediaUpload.Application.DTOs;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Interfaces;
using MediaUpload.Domain.Enums;
using MediaUpload.API.Middleware;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/config/timewindows")]
[RequirePermission("config")]
public class TimeWindowController(ITimeWindowConfigRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok((await repo.GetAllAsync()).Select(Map));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await repo.GetByIdAsync(id);
        return c == null ? NotFound() : Ok(Map(c));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TimeWindowRequest req)
    {
        var c = new TimeWindowConfig
        {
            Name       = req.Name,
            StartTime  = TimeOnly.Parse(req.StartTime),
            EndTime    = TimeOnly.Parse(req.EndTime),
            DaysOfWeek = req.DaysOfWeek,
            Enabled    = req.Enabled,
        };
        await repo.AddAsync(c);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, Map(c));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TimeWindowRequest req)
    {
        var c = await repo.GetByIdAsync(id);
        if (c == null) return NotFound();
        c.Name       = req.Name;
        c.StartTime  = TimeOnly.Parse(req.StartTime);
        c.EndTime    = TimeOnly.Parse(req.EndTime);
        c.DaysOfWeek = req.DaysOfWeek;
        c.Enabled    = req.Enabled;
        await repo.UpdateAsync(c);
        return Ok(Map(c));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id);
        return NoContent();
    }

    private static TimeWindowDto Map(TimeWindowConfig c) => new(
        c.Id, c.Name,
        c.StartTime.ToString("HH:mm"),
        c.EndTime.ToString("HH:mm"),
        c.DaysOfWeek, c.Enabled);
}

[ApiController]
[Route("api/config/erp")]
[RequirePermission("config")]
public class ErpConfigController(
    IErpEndpointConfigRepository repo,
    Domain.Interfaces.IEncryptionService encryption) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await repo.GetAllAsync();
        return Ok(list.Select(e => new ErpEndpointDto(
            e.Id, e.Target, e.Url,
            TokenPrefix: e.EncryptedToken.Length >= 8 ? e.EncryptedToken[..8] + "..." : "***",
            e.Enabled)));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] ErpEndpointRequest req)
    {
        var c = new ErpEndpointConfig
        {
            Target         = req.Target.ToUpper(),
            Url            = req.Url,
            EncryptedToken = encryption.Encrypt(req.Token),
            Enabled        = req.Enabled,
        };
        await repo.UpsertAsync(c);
        return Ok(new ErpEndpointDto(c.Id, c.Target, c.Url,
            c.EncryptedToken[..Math.Min(8, c.EncryptedToken.Length)] + "...", c.Enabled));
    }
}

[ApiController]
[Route("api/config/settings")]
[RequirePermission("config")]
public class SystemSettingsController(
    ISystemSettingRepository settingRepo,
    Application.Services.SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await settingRepo.GetAllAsync();
        return Ok(list.Select(s => new SettingDto(
            s.Key, s.Value, s.Description, s.HotReload, s.UpdatedAtUtc.ToString("O"))));
    }

    [HttpPatch]
    public async Task<IActionResult> Patch([FromBody] PatchSettingsRequest req)
    {
        foreach (var kv in req.Updates)
            await settingsService.SetAsync(kv.Key, kv.Value);
        return Ok(new { updated = req.Updates.Keys });
    }

    [HttpPost("reset/{key}")]
    public async Task<IActionResult> Reset(string key)
    {
        await settingRepo.ResetToDefaultAsync(key);
        settingsService.Invalidate(key);
        var val = await settingRepo.GetAsync(key);
        return Ok(new { key, value = val });
    }
}
