using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WTW.CET.AFD.WebApp.Interfaces;

namespace WTW.CET.AFD.WebApp.Controllers
{
  public class AFDController : Controller
  {
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AFDController(IHttpContextAccessor httpContextAccessor)
    {
      _httpContextAccessor = httpContextAccessor;
    }

    public IActionResult Index([FromServices]IAppRepository appRepository)
    {
      // Docs here: https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-2.2
      // Say that the X-Forwarded-Host header value should override this if using the Forwarded Headers Middleware
      ViewData["HostName"] = _httpContextAccessor.HttpContext.Request.Host;
      ViewData["Method"] = _httpContextAccessor.HttpContext.Request.Method;
      ViewData["Scheme"] = _httpContextAccessor.HttpContext.Request.Scheme;
      ViewData["Path"] = _httpContextAccessor.HttpContext.Request.Path;

      var headers = new Dictionary<string, string>();
      foreach (var (key, value) in _httpContextAccessor.HttpContext.Request.Headers)
      {
        headers.Add(key, value);
      }

      ViewData["Headers"] = headers;
      ViewData["RemoteIp"] = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress;

      ViewData["AllowedAzureFDIDs"] = appRepository.AllowedAzureFDIDs == null ? "" : string.Join(";", appRepository.AllowedAzureFDIDs);

      ViewData["HealthProbePath"] = appRepository.HealthProbePath;

      ViewData["OriginalContexts"] = appRepository.HttpContexts;

      return View();
    }
  }
}