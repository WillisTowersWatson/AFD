using System.Collections.Generic;

namespace WTW.CET.AFD.WebApp.Interfaces
{
  /// <summary>
  /// Singleton application repository
  /// </summary>
  public interface IAppRepository
  {
    IList<string> AllowedFrontEndHosts { get; set; }
    string HealthProbePath { get; set; }

    IList<HttpContextCacheEntry> HttpContexts { get; }
  }
}
