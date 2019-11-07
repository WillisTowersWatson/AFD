using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading.Tasks;

namespace WTW.CET.AFD.Middleware
{
  public class AzureFrontDoorMiddleware
  {
    private readonly AzureFrontDoorOptions _options;
    private readonly RequestDelegate _next;

    public AzureFrontDoorMiddleware(RequestDelegate next, IOptions<AzureFrontDoorOptions> options)
    {
      if (options == null)
        throw new ArgumentNullException(nameof(options));

      _options = options.Value;
      _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task Invoke(HttpContext context)
    {
      ApplyAzureFrontDoor(context);
      return _next(context);
    }

    public void ApplyAzureFrontDoor(HttpContext context)
    {
      // 1. Process the health probe request using specialised logic
      if (context.Request.Headers["X-FD-HealthProbe"] == "1")
      {
        // check for bad requests to the health probe
        if (
          // Only allow probes on our recognised path
          (_options.HealthProbePath != null && !string.Equals(context.Request.Path, _options.HealthProbePath, StringComparison.OrdinalIgnoreCase))

          // Health probes come from internal AFD systems so legitimate probes WILL NOT have X-Forwarded-Host header set
          // NOTE: Other X-Forwarded-* headers may be set
          || context.Request.Headers.ContainsKey(ForwardedHeadersDefaults.XForwardedHostHeaderName))
        {
          // Return a Bad Request and short-circuit any more processing
          context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          return;
        }

        // Simulate the probe coming from our AFD so that subsequent 'AllowHost' processing can continue as expected
        context.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = _options.DefaultProbeFrontEnd;
      }
    }
  }
}