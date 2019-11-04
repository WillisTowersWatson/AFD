using System.Collections.Generic;
using wtw.cet.afd.webapp.Interfaces;

namespace wtw.cet.afd.webapp
{
  /// <summary>
  /// Singleton application repository
  /// </summary>
  public class AppRepository : IAppRepository
  {
    public IList<string> AllowedHosts { get; set; }
    public IList<string> AllowedForwardedHosts { get; set; }
  }
}
