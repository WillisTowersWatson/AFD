using Microsoft.AspNetCore.Http;
using System.Linq;

namespace WTW.CET.AFD.WebApp.Interfaces
{
  public class HttpContextCacheEntry
  {
    public HostString? Host { get; private set; }
    public PathString Path { get; }
    public IHeaderDictionary RequestHeaders { get; private set; }

    public HttpContextCacheEntry(HttpContext ctx)
    {
      Host = ctx.Request.Host;
      Path = ctx.Request.Path;

      // clone the request headers
      RequestHeaders = new HeaderDictionary(ctx.Request.Headers.ToDictionary(e => e.Key, e => e.Value));
    }
  }
}
