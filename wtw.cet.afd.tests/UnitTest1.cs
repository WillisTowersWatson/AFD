using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCoreTests
{
  public class XForwardedForTests
  {
    [Fact]
    public async Task CheckForwardedForIsConsumed()
    {
      var builder = new WebHostBuilder()
               .Configure(app =>
               {
                 app.UseForwardedHeaders(new ForwardedHeadersOptions
                 {
                   ForwardedHeaders = ForwardedHeaders.XForwardedFor
                 });
               });
      var server = new TestServer(builder);

      var context = await server.SendAsync(c =>
      {
        c.Request.Headers["X-Forwarded-For"] = "11.111.111.11:9090";
      });

      Assert.Equal("11.111.111.11", context.Connection.RemoteIpAddress.ToString());
      Assert.Equal(9090, context.Connection.RemotePort);
      // No Original set if RemoteIpAddress started null.
      Assert.False(context.Request.Headers.ContainsKey("X-Original-For"));
      // Should have been consumed and removed
      Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-For"));
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

    //[Theory]
    //[InlineData("myapp.azurewebsites.net:443", "My.Front.Door.net")]
    //public async Task RejectXForwardedFromUnknownSource(string host, string allowedHost)
    //{
    //  throw new NotImplementedException("Awaiting implementation");
    //}
  }
}
