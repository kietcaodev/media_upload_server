using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaUpload.Infrastructure.Persistence;

/// <summary>
/// Used only by EF Core CLI tools (dotnet ef migrations add, etc.).
/// Connection string here is just for design-time; not used at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opt = new DbContextOptionsBuilder<AppDbContext>();
        opt.UseNpgsql("Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=design_time_only");
        return new AppDbContext(opt.Options);
    }
}
