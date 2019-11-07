// #define IncludeToddIssues

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Net.Http.Headers;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WTW.CET.AFD.Middleware;
using Xunit;

namespace WTW.CET.AFD.Tests
{
  /// <summary>
  /// Tests to help me understand the support for X-Forwarded-* headers
  /// </summary>
  public partial class XForwardedForTests
  {
    /// <summary>
    /// Secure our app which is a backend to Azure Front Door.
    /// We need to inspect headers in order to ensure this backend isn't being access from a spoof AFD.
    /// </summary>
    /// <param name="xForwardedHost">The simulated value of X-Forwarded-Host</param>
    /// <param name="requestHost">The requested HOST</param>
    /// <param name="probePath">If not null this is a Probe simulation and this is the path being probed</param>
    /// <param name="expectedCode">The expected status code for this request</param>
    [Theory]
    [ClassData(typeof(XForwardedForTestData))]
    public async Task SecureAccessCheckingWithMiddleware(string xForwardedHost, string requestHost, string? probePath, HttpStatusCode expectedCode)
    {
      var builder = new WebHostBuilder()
          .ConfigureServices(services =>
          {
            // Configure the AFD service
            // NOTE: 
            services.AddAzureFrontDoor((options) =>
            {
              // Only allow our AFD front-ends to be proxies
              options.AllowedFrontEndHosts = XForwardedForTestData.AllowedFrontEndHosts;
              // Need to identify the health probe path as needs special processing
              options.HealthProbePath = XForwardedForTestData.HealthProbePath;
            });
          })

          .Configure(app =>
          {
            app.UseAzureFrontDoor();

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            ////// Now do some testing
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // Assert that the HOST is set to the expected AFD
            app.Use(async (context, next) =>
            {
              // At this point the HOST should be set to one of the front ends
              Assert.Contains<string>(context.Request.Headers[HeaderNames.Host], XForwardedForTestData.AllowedFrontEndHosts, StringComparer.OrdinalIgnoreCase);
              await next();
            });

            // Simulate only having recognised endpoints of XForwardedForTestData.HEALTH_PROBE_PATH and null
            app.UseWhen(ctx =>
              ctx.Response.StatusCode == (int)HttpStatusCode.OK
              && !string.Equals(ctx.Request.Path, XForwardedForTestData.HealthProbePath, StringComparison.OrdinalIgnoreCase)
              && ctx.Request.Path != null,
            (app) => app.Use(async (ctx, next) =>
            {
              ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
              await next();
            }));

            // Run the tests
            app.Run(c => Task.CompletedTask);
          });


      // Build the test server to perform the tests against
      var server = new TestServer(builder);

      {
        var response = await server.SendAsync(ctx =>
        {
          ctx.Request.Headers[HeaderNames.Host] = requestHost;
          ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = xForwardedHost;
          if (probePath != null)
          {
            // Check access is restricted to the health probe path if we're probing
            ctx.Request.Path = probePath;
            ctx.Request.Headers["X-FD-HealthProbe"] = "1";
          }
        });

        Assert.Equal((int)expectedCode, response.Response.StatusCode);
      }
    }

    /// <summary>
    /// Secure our app which is a backend to Azure Front Door.
    /// We need to inspect headers in order to ensure this backend isn't being access from a spoof AFD.
    /// </summary>
    /// <param name="xForwardedHost">The simulated value of X-Forwarded-Host</param>
    /// <param name="requestHost">The requested HOST</param>
    /// <param name="probePath">If not null this is a Probe simulation and this is the path being probed</param>
    /// <param name="expectedCode">The expected status code for this request</param>
    [Theory]
    [ClassData(typeof(XForwardedForTestData))]
    public async Task SecureAccessChecking(string xForwardedHost, string requestHost, string? probePath, HttpStatusCode expectedCode)
    {
      var builder = new WebHostBuilder()
          .ConfigureServices(services =>
          {
            services.AddHostFiltering(options =>
            {
              options.AllowEmptyHosts = false;
              options.AllowedHosts = XForwardedForTestData.AllowedFrontEndHosts;
            });
          })

          .Configure(app =>
          {
            // 1. Process the health probe request using specialised logic
            app.UseWhen(ctx => ctx.Request.Headers["X-FD-HealthProbe"] == "1",
              (app) => app.Use(async (ctx, next) =>
              {
                // check for bad requests to the health probe
                if (
                  // Only allow probes on our recognised path
                  !string.Equals(ctx.Request.Path, XForwardedForTestData.HealthProbePath, StringComparison.OrdinalIgnoreCase)

                  // Health probes come from internal AFD systems so legitimate probes WILL NOT have X-Forwarded-* headers
                  || !string.IsNullOrEmpty(ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName]))
                {
                  // Return a Bad Request and short-circuit any more processing
                  ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                  //return Task.CompletedTask;
                  return;
                }

                // Simulate the probe coming from our AFD so that subsequent 'AllowHost' processing can continue as expected
                // TODO: Map from HOST to AFD
                ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = XForwardedForTestData.AllowedFrontEndHosts.FirstOrDefault();

                // or we could repond immediately instead of hitting the endpoint
                // await ctx.Response.WriteAsync("OK");

                //return Task.CompletedTask;
                await next();
              }));

            // 2. Configure forwarded headers middleware to only process headers from our expected forwarder
            {
              var options = new ForwardedHeadersOptions
              {
                ForwardedHeaders = ForwardedHeaders.All,

                // only allow these hosts as forwarder to be transferred to HOST
                AllowedHosts = XForwardedForTestData.AllowedFrontEndHosts
              };
              options.KnownProxies.Clear();
              options.KnownNetworks.Clear();

              app.UseForwardedHeaders(options);
            }

            // 3. Only allow our AFDs as a host
            app.UseHostFiltering();

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            ////// Now do some testing
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // Assert that the HOST is set to the expected AFD
            app.Use(async (context, next) =>
            {
              // At this point the HOST should be set to one of the front ends
              Assert.Contains<string>(context.Request.Headers[HeaderNames.Host], XForwardedForTestData.AllowedFrontEndHosts, StringComparer.OrdinalIgnoreCase);
              await next();
            });

            // Simulate only having recognised endpoints of XForwardedForTestData.HEALTH_PROBE_PATH and null
            app.UseWhen(ctx =>
              ctx.Response.StatusCode == (int)HttpStatusCode.OK
              && !string.Equals(ctx.Request.Path, XForwardedForTestData.HealthProbePath, StringComparison.OrdinalIgnoreCase)
              && ctx.Request.Path != null,
            (app) => app.Use(async (ctx, next) =>
            {
              ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
              await next();
            }));

            // Run the tests
            app.Run(c => Task.CompletedTask);
          });


      // Build the test server to perform the tests against
      var server = new TestServer(builder);

      {
        var response = await server.SendAsync(ctx =>
        {
          ctx.Request.Headers[HeaderNames.Host] = requestHost;
          ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = xForwardedHost;
          if (probePath != null)
          {
            // Check access is restricted to the health probe path if we're probing
            ctx.Request.Path = probePath;
            ctx.Request.Headers["X-FD-HealthProbe"] = "1";
          }
        });

        Assert.Equal((int)expectedCode, response.Response.StatusCode);
      }
    }

    [Theory]
    [InlineData("my.front.door.net:443", "My.Front.Door.net")]
    public async Task MatchingXForwardedHostHeader(string host, string allowedHost)
    {
      bool assertsExecuted = false;
      var builder = new WebHostBuilder()
          .Configure(app =>
          {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
              ForwardedHeaders = ForwardedHeaders.XForwardedHost,
              AllowedHosts = allowedHost.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            });
            app.Run(context =>
            {
              // Host is allowed so should now be the X-Forwarded-host
              Assert.Equal(host, context.Request.Headers[HeaderNames.Host]);

              // X-Original-Host will be the original (localhost) host

              assertsExecuted = true;
              return Task.FromResult(0);
            });
          });
      var server = new TestServer(builder);
      var response = await server.SendAsync(ctx =>
      {
        ctx.Request.Headers["X-forwarded-Host"] = host;
      });
      Assert.True(assertsExecuted);
    }

    [Theory]
    [InlineData("spoof.front.door.net:443", "My.Front.Door.net")]
    public async Task CheckIgnoreXForwardedFromUnknownSource(string host, string allowedHost)
    {
      bool assertsExecuted = false;
      var builder = new WebHostBuilder()
          .Configure(app =>
          {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
              ForwardedHeaders = ForwardedHeaders.XForwardedHost,
              AllowedHosts = allowedHost.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            });
            app.Run(context =>
            {
              // Check that the host has NOT been set to the host
              Assert.NotEqual<string>(host, context.Request.Headers[HeaderNames.Host]);
              // Check that the X-forwarded-Host has stayed the same (i.e. host)
              Assert.Equal(host, context.Request.Headers["X-forwarded-Host"]);
              assertsExecuted = true;
              return Task.CompletedTask;
            });
          });
      var server = new TestServer(builder);
      var response = await server.SendAsync(ctx =>
      {
        ctx.Request.Headers["X-forwarded-Host"] = host;
      });
      Assert.True(assertsExecuted);
    }

    [Theory(DisplayName = "Test the technique suggested by Todd")]

    // happy path
    [InlineData("MY.Front.Door.net", false, HttpStatusCode.OK)]

    // someone is using another AFD to access the backend
    [InlineData("spoof.front.door.net", false, HttpStatusCode.BadRequest)]

    // a spoof proxy is probing (requests by *their* AFD probing mechanism)
    // TODO: How can we tell?
    // [InlineData(null, true, HttpStatusCode.BadRequest)]

#if IncludeToddIssues
    // our AFD is probing (requests by the AFD probing mechanism)
    [InlineData(null, true, HttpStatusCode.OK)]

    // someone is using our AFD to spoof a probe by injecting X-FD-HealthProbe headers
    // TODO: Check if AFD strips this from headers before passing on
    [InlineData("my.front.door.net", true, HttpStatusCode.BadRequest)]
#endif

    // a spoof proxy is being used to probe (requests via their AFD for example)
    [InlineData("SPOOF.Front.Door.net", true, HttpStatusCode.BadRequest)]

    public async Task CheckToddSuggestion(string xForwardedHost, bool probe, HttpStatusCode expectedCode)
    {
      const string AFD_XFORWARD_VALUE = "my.front.door.net";

      var builder = new WebHostBuilder()
          .Configure(app =>
          {
            app.Use((context, next) =>
            {
              //if (string.Equals(context.Request.Headers["X-Forwarded-Host"], Environment.GetEnvironmentVariable("AFD_XFORWARD_VALUE"), StringComparison.OrdinalIgnoreCase))
              if (string.Equals(context.Request.Headers["X-Forwarded-Host"], AFD_XFORWARD_VALUE, StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

              //context.Response.WriteAsync($"No valid AFD header found.{Environment.NewLine}");
              context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
              return next();
            });

            app.Run(c => Task.CompletedTask);
          });

      var server = new TestServer(builder);
      {
        var response = await server.SendAsync(ctx =>
        {
          ctx.Request.Headers["X-forwarded-Host"] = xForwardedHost;
          if (probe)
            ctx.Request.Headers["X-FD-HealthProbe"] = "1";
        });

        Assert.Equal((int)expectedCode, response.Response.StatusCode);
      }
    }
  }
}