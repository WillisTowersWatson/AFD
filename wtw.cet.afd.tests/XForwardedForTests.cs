// #define IncludeToddIssues

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCoreTests
{
  /// <summary>
  /// Tests to help me understand the support for X-Forwarded-* headers
  /// </summary>
  public class XForwardedForTests
  {
    // TODO: Parameterise this/pull them from configuration info
    private readonly List<string> AFD_FQDNs = new List<string>() { "my.front.door.net" };
    private const string HEALTH_PROBE_PATH = "/HealthProbe";

    private const string BAD_HEALTH_PROBE_PATH = "/PokeTheApp";

    private void HandleHealthProbe(IApplicationBuilder app)
    {
      //const string XHandledHealthProbeHeaderName = "X-HandledHealthProbe";

      app.Run(ctx =>
      {
        // 1. Health probes come from internal AFD systems so legitimate probes won't have X-Forwarded-* headers
        if (!string.IsNullOrEmpty(ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName])
              || !string.IsNullOrEmpty(ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName])
              || !string.IsNullOrEmpty(ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedProtoHeaderName]))
        {
          ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          return Task.CompletedTask;
        }

        // Expecting HOST to be <this app>.azurewebsites.net
        // TODO: Only allow this to continue IF the host is what we 'expect' <<< does this make sense?
        // Assert.Equal(ctx.Request.Headers[HeaderNames.Host], <this app>.azurewebsites.net)

        // 2. Simulate the probe coming from our AFD so that subsequent 'AllowHost' processing can continue as expected
        // TODO: Map from HOST to AFD
        ctx.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = AFD_FQDNs.First();

        return Task.CompletedTask;
      });
    }

    [Theory]

    // happy path
    [InlineData("MY.Front.Door.net", "MY.Front.Door.net", null, HttpStatusCode.OK)]

    // our AFD is probing (requests by the AFD probing mechanism)
    [InlineData(null, "my.azurewebsite.net", HEALTH_PROBE_PATH, HttpStatusCode.OK)]

    // our AFD is probing the wrong path!
    [InlineData(null, "my.azurewebsite.net", BAD_HEALTH_PROBE_PATH, HttpStatusCode.BadRequest)]

    // someone is using another AFD to access the backend
    [InlineData("spoof.front.door.net", "my.azurewebsite.net", null, HttpStatusCode.BadRequest)]

    // someone is using our AFD to spoof a probe by injecting X-FD-HealthProbe headers
    // TODO: Check to see if AFD strips this from headers before passing it on
    [InlineData("my.front.door.net", "MY.Front.Door.net", HEALTH_PROBE_PATH, HttpStatusCode.BadRequest)]

    // a spoof proxy is being used to probe (requests via their AFD for example)
    [InlineData("SPOOF.Front.Door.net", "my.azurewebsite.net", HEALTH_PROBE_PATH, HttpStatusCode.BadRequest)]

    // a hacker is using their probe as a way of accessing/doing a ddos attack on a poorly secured app which may allow any probes through
    [InlineData("SPOOF.Front.Door.net", "my.azurewebsite.net", BAD_HEALTH_PROBE_PATH, HttpStatusCode.BadRequest)]

    public async Task SecureAccessChecking(string xForwardedHost, string requestHost, string? probePath, HttpStatusCode expectedCode)
    {
      var builder = new WebHostBuilder()
          // Only allow our AFDs as a host
          .ConfigureServices(services =>
          {
            services.AddHostFiltering(options =>
            {
              options.AllowEmptyHosts = false;
              options.AllowedHosts = AFD_FQDNs;
            });
          })

          .Configure(app =>
          {
            // 1. Process the health probe request using specialised logic
            app.MapWhen(ctx =>
                string.Equals(ctx.Request.Headers["X-FD-HealthProbe"], "1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(ctx.Request.Path, HEALTH_PROBE_PATH, StringComparison.OrdinalIgnoreCase),
              HandleHealthProbe);

            // 2. Configure forwarded headers middleware to only process headers from our expected forwarder
            {
              var options = new ForwardedHeadersOptions
              {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor,

                // only allow AFD_XFORWARD_VALUE as a forwarder to be transferred to HOST
                AllowedHosts = AFD_FQDNs
              };
              options.KnownProxies.Clear();
              options.KnownNetworks.Clear();

              app.UseForwardedHeaders(options);
            }

            // 3. Only allow our AFDs as a host
            app.UseHostFiltering();

            // Assert that the HOST is set to the expected AFD
            app.Use((context, next) =>
            {
              Assert.Contains<string>(context.Request.Headers[HeaderNames.Host], AFD_FQDNs, StringComparer.OrdinalIgnoreCase);
              return Task.CompletedTask;
            });

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
        Assert.Equal(response.Response.StatusCode, (int)expectedCode);
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

        Assert.Equal(response.Response.StatusCode, (int)expectedCode);
      }
    }
  }
}