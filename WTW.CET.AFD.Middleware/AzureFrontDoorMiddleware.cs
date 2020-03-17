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
      var afdId = context.Request.Headers["X-AzureFDID"];

      // 1. Process the health probe request using specialised logic
      if (string.IsNullOrEmpty(afdId))
      {
        // Return a Bad Request and short-circuit any more processing
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
      }

      if(_options.)
    }
  }
}