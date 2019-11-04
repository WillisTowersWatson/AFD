using System.Collections.Generic;

namespace wtw.cet.afd.webapp.Interfaces
{
  /// <summary>
  /// Singleton application repository
  /// </summary>
  public interface IAppRepository
  {
    IList<string> AllowedHosts { get; set; }
    IList<string> AllowedForwardedHosts { get; set; }
  }
}
