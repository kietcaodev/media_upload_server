using MediaUpload.Application;
using MediaUpload.Infrastructure;
using MediaUpload.Infrastructure.Persistence;
using MediaUpload.Domain.Interfaces;
using MediaUpload.API.Hubs;
using MediaUpload.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpClient("erp");
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS – origins from appsettings only at startup; runtime changes via DB settings
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins)
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
}

// ── Middleware pipeline ───────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<AuthMiddleware>();
app.MapControllers();
app.MapHub<JobHub>("/hubs/jobs");
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
