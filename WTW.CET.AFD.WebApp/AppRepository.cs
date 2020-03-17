using System.Collections.Generic;
using WTW.CET.AFD.WebApp.Interfaces;

namespace WTW.CET.AFD.WebApp
{
  /// <summary>
  /// Singleton application repository
  /// </summary>
  public class AppRepository : IAppRepository
  {
    public IList<string>? AllowedAzureFDIDs { get; set; }

    public string? HealthProbePath { get; set; }

    public IList<HttpContextCacheEntry> HttpContexts { get; } = new List<HttpContextCacheEntry>();
  }
}
