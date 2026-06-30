using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MediaUpload.API.Middleware;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute(string permission) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items["credential"] is not ApiCredential cred)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Not authenticated" });
            return;
        }

        bool allowed = permission switch
        {
            "upload"    => cred.CanUpload,
            "read_jobs" => cred.CanReadJobs,
            "config"    => cred.CanConfig,
            _ => false
        };

        if (!allowed)
        {
            context.Result = new ObjectResult(new { error = $"Permission '{permission}' required" })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
