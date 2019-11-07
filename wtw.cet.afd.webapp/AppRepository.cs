using System.Collections.Generic;
using WTW.CET.AFD.WebApp.Interfaces;

namespace WTW.CET.AFD.WebApp
{
  /// <summary>
  /// Singleton application repository
  /// </summary>
  public class AppRepository : IAppRepository
  {
#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
    public IList<string>? AllowedFrontEndHosts { get; set; }
    public string? HealthProbePath { get; set; }
#pragma warning restore CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.

    public IList<HttpContextCacheEntry> HttpContexts { get; } = new List<HttpContextCacheEntry>();
  }
}
