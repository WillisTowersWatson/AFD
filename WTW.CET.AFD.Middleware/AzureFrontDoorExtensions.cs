using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace WTW.CET.AFD.Middleware
{
  public static partial class AzureFrontDoorExtensions
  {
    /// <summary>
    /// Adds the Azure Front Door middleware.
    /// </summary>
    /// <remarks>
    /// Also configures the Host Filtering middleware to only allow the <see cref="AzureFrontDoorOptions"/> AllowedHosts list of hosts.
    /// </remarks>
    /// <param name="services"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddAzureFrontDoor(this IServiceCollection services, Action<AzureFrontDoorOptions> configureOptions)
    {
      if (services == null) throw new ArgumentNullException(nameof(services));
      if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

      var afdOptions = new AzureFrontDoorOptions(configureOptions);

      services.Configure<AFDHostFilteringOptions>(options =>
      {
        options.AllowEmptyHosts = false;
        options.AllowedHosts = afdOptions.AllowedFrontEndHosts;
      });

      services.Configure(configureOptions);

      return services;
    }

    /// <summary>
    /// Update the IApplicationBuilder to be Azure Front Door aware such that:
    ///   Add a branch in the request pipeline such that if an AFD probe is being performed:
    ///     If the path isn't the (optional) <paramref name="healthProbePath"/> OR there's an X-Forwarded-Host header
    ///       Return a response with StatusCode 400
    ///       
    ///     Spoof an X-Forwarded-Host entry as the first of the <paramref name="frontEndFqdns"/> so subsequent 'AllowHost' checks succeed
    ///   
    ///   Set the Forwarded Headers (see <see cref="ForwardedHeadersExtensions.UseForwardedHeaders(IApplicationBuilder)"/>) to forward proxy headers IFF it's one of <paramref name="frontEndFqdns"/>
    ///   
    ///   Set the Host Filter (see <seealso cref="HostFilteringBuilderExtensions.UseHostFiltering(IApplicationBuilder)"/> to deny access to 
    /// </summary>
    /// <remarks>
    ///   Uses Forwarded Headers and Host Filtering middleware so do NOT include these in the pipeline if using this middleware.
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="frontEndFqdns"></param>
    /// <param name="healthProbePath"></param>
    public static IApplicationBuilder UseAzureFrontDoor(this IApplicationBuilder app)
    {
      if (app == null) throw new ArgumentNullException(nameof(app));

      //var afdOptions = app.ApplicationServices.GetService<AzureFrontDoorOptions>();
      var afdOptions = app.ApplicationServices.GetService<IOptions<AzureFrontDoorOptions>>();
      if (afdOptions == null) throw new ArgumentException(nameof(afdOptions));

      // Use our AFD Middleware to do some health-probe processing
      app.UseMiddleware<AzureFrontDoorMiddleware>();

      // Use forwarded headers middleware to only process headers from our expected forwarder
      app.UseForwardedHeaders(afdOptions.Value.ForwardedHeadersOptions);

      // Mimic app.UseHostFiltering() to ensure the host matches one of our AFD front-ends
      // NOTE: Can't use app.UseHostFiltering() directly as this uses a single set of options AND the Host Filtering middleware is injected before this is called so we won't ever reach here
      app.UseMiddleware<AFDHostFilteringMiddleware>();

      return app;
    }
  }

  public class AFDHostFilteringOptions : HostFilteringOptions
  {
  }

  public class AFDHostFilteringMiddleware : HostFilteringMiddleware
  {
    public AFDHostFilteringMiddleware(RequestDelegate next, ILogger<AFDHostFilteringMiddleware> logger,
          IOptionsMonitor<AFDHostFilteringOptions> optionsMonitor) : base(next, logger, optionsMonitor)
    {
    }
  }
}