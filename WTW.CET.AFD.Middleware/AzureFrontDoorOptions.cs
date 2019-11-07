using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WTW.CET.AFD.Middleware
{
  public class AzureFrontDoorOptions
  {
    public IList<string>? AllowedFrontEndHosts
    {
      get
      {
        return ForwardedHeadersOptions.AllowedHosts;
      }

      set
      {
        ForwardedHeadersOptions.AllowedHosts = value;
      }
    }

    public string? HealthProbePath { get; set; }
    public ForwardedHeadersOptions ForwardedHeadersOptions { get; private set; }

    /// <summary>
    /// Pick a default front-end as we're being probed and we need to fake a valid X-Header-Host
    /// </summary>
    /// <remark>
    /// TODO: Consider if we need to do anything more sophisticated here
    /// </remark>
    public StringValues DefaultProbeFrontEnd
    {
      get
      {
        return AllowedFrontEndHosts?.FirstOrDefault();
      }
    }

    public AzureFrontDoorOptions()
    {
      ForwardedHeadersOptions = new ForwardedHeadersOptions
      {
        ForwardedHeaders = ForwardedHeaders.All,

        // only allow these front ends as a forwarder to be transferred to HOST
        AllowedHosts = null // AllowedFrontEndHosts
      };

      ForwardedHeadersOptions.KnownProxies.Clear();
      ForwardedHeadersOptions.KnownNetworks.Clear();
    }

    public AzureFrontDoorOptions(Action<AzureFrontDoorOptions> configureOptions) : this()
    {
      configureOptions(this);
      Validate();
    }

    internal void Validate()
    {
      if (AllowedFrontEndHosts == null) throw new ArgumentNullException(nameof(AllowedFrontEndHosts));
      if (HealthProbePath == null) throw new ArgumentNullException(nameof(HealthProbePath));
      if (ForwardedHeadersOptions == null) throw new ArgumentNullException(nameof(ForwardedHeadersOptions));
    }
  }
}