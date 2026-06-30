using Microsoft.Extensions.DependencyInjection;
using MediaUpload.Application.Services;
using MediaUpload.Application.Worker;

namespace MediaUpload.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SettingsService>();
        services.AddScoped<TimeWindowChecker>();
        services.AddScoped<ErpPushService>();

        services.AddSingleton<WorkerStateService>();
        services.AddHostedService<JobWorkerService>();

        return services;
    }
}
