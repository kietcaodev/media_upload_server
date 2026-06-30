using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediaUpload.Domain.Interfaces;
using MediaUpload.Infrastructure.Persistence;
using MediaUpload.Infrastructure.Repositories;
using MediaUpload.Infrastructure.Services;

namespace MediaUpload.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default")));

        services.AddScoped<IUploadJobRepository, UploadJobRepository>();
        services.AddScoped<ITimeWindowConfigRepository, TimeWindowConfigRepository>();
        services.AddScoped<IErpEndpointConfigRepository, ErpEndpointConfigRepository>();
        services.AddScoped<IApiCredentialRepository, ApiCredentialRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

        var aesKey = config["Encryption:AesKey"]
            ?? throw new InvalidOperationException("Encryption:AesKey is required in appsettings.");
        services.AddSingleton<IEncryptionService>(new AesEncryptionService(aesKey));

        return services;
    }
}
