using MediaUpload.Application;
using MediaUpload.Application.Services;
using MediaUpload.Infrastructure;
using MediaUpload.Infrastructure.Persistence;
using MediaUpload.Domain.Interfaces;
using MediaUpload.API.Hubs;
using MediaUpload.API.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ── Logging: ghi ra file thật (app-*.log toàn bộ, error-*.log riêng lỗi) bên
// cạnh Console (giữ nguyên để journalctl vẫn xem được như trước). Thư mục log
// mặc định nằm NGOÀI ContentRootPath (.../api/../logs) để KHÔNG bị xoá mỗi lần
// `dotnet publish` ghi đè lại thư mục api/ lúc redeploy. Có thể override bằng
// đường dẫn tuyệt đối qua config "Logging:Directory" (deploy.sh set sẵn).
var logDirectory = builder.Configuration["Logging:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "logs");
Directory.CreateDirectory(logDirectory);

builder.Host.UseSerilog((_, loggerConfig) =>
{
    loggerConfig
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "app.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true)
        .WriteTo.File(
            Path.Combine(logDirectory, "error.log"),
            restrictedToMinimumLevel: LogEventLevel.Error,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true);
});

// ── Services ─────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpClient("erp");
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS – seeded from appsettings as a fallback, then overridden from the DB
// (cors.allowed_origins) below. The origin check reads RuntimeConfigCache on
// every request, so admin edits via System Settings take effect immediately.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];
RuntimeConfigCache.SetCorsAllowedOrigins(corsOrigins);

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(RuntimeConfigCache.IsCorsOriginAllowed)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

var app = builder.Build();

// ── Auto-migrate + startup tasks ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var jobRepo = scope.ServiceProvider.GetRequiredService<IUploadJobRepository>();
    await jobRepo.ResetStuckJobsAsync();

    // Load DB-backed settings that must be checked synchronously per-request
    // (CORS origins, rate limit) into the runtime cache.
    var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
    RuntimeConfigCache.SetCorsAllowedOrigins(await settingsService.GetListAsync("cors.allowed_origins"));
    RuntimeConfigCache.SetRateLimit(
        await settingsService.GetIntAsync("ratelimit.window_ms", 900_000),
        await settingsService.GetIntAsync("ratelimit.max_requests", 20));
}

// ── Middleware pipeline ───────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<UploadRateLimitMiddleware>();
app.MapControllers();
app.MapHub<JobHub>("/hubs/jobs");
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
