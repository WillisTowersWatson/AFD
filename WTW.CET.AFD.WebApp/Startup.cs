using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using WTW.CET.AFD.WebApp.Interfaces;

namespace WTW.CET.AFD.WebApp
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime.  this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddControllersWithViews();

      // enable access to the HttpContext for display purposes
      services.AddHttpContextAccessor();

      /////////////////////////////////////////////////

      // Create and register our app repo singleton
      var appRepository = new AppRepository();
      {
        IList<string>? getList(string appSettingName)
        {
          return Configuration.GetValue<string>(appSettingName)?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        appRepository.AllowedAzureFDIDs = getList("AFD.AllowedAzureFDIDs");
        appRepository.HealthProbePath = Configuration.GetValue<string>("HealthProbePath");

        services.AddSingleton<IAppRepository>(appRepository);
      }

      // add health checking
      services.AddHealthChecks();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reflection will not allow this to be static")]
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IAppRepository appRepository)
    {
      ///////////////////////////////////////////////
      /// START: Specific AFD processing
      ///////////////////////////////////////////////

      // Pick out any Azure Front Door ID from the headers
      static string getAFDID(HttpContext context)
      {
        return context.Request.Headers["X-Azure-FDID"];
      }

      // Introduce some middleware to inject the id into the response headers for debugging purposes
      app.Use(async (context, next) =>
      {
        context.Response.OnStarting(() =>
        {
          context.Response.Headers["X-Response-Azure-FDID"] = getAFDID(context);
          return Task.FromResult(0);
        });

        await next();
      });

      // Map a special path to grab the Azure Front Door Id
      // NOTE: We're waiting for the Azure Front Door API to include getting this info so we don't need to surface it this way in the future
      app.Map("/fdid", app =>
      {
        // Respond with the FD ID in the body and as a new header (see the above middleware)
        app.Run(async context => {
          await context.Response.WriteAsync(JsonSerializer.Serialize(new { AzureFDID = getAFDID(context) }));
        });
      });

      // Intercept requests and check for Azure FD usage
      app.Use(async (context, next) =>
      {
        // If we're restricting this app to specific Azure Front Door(s)...
        if (appRepository.AllowedAzureFDIDs != null)
        {
          // ... pick out the Front Door ID from the headers ...
          string afdId = getAFDID(context);

          // ... and return a 'Bad request' if there's no id or it's not one of the allowed ids
          if (string.IsNullOrEmpty(afdId) || !appRepository.AllowedAzureFDIDs.Contains(afdId, StringComparer.OrdinalIgnoreCase))
          {
            // Return a Bad Request and short-circuit any more processing
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
          }
        }

        // All good, move onto the next middleware service
        await next();
      });

      ///////////////////////////////////////////////
      /// END: Specific AFD processing
      ///////////////////////////////////////////////

      // Use optional health checks middleware to provide an endpoint for the AFD probe
      if (!string.IsNullOrEmpty(appRepository.HealthProbePath)) app.UseHealthChecks(appRepository.HealthProbePath);

      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }
      else
      {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
      }
      app.UseHttpsRedirection();
      app.UseStaticFiles();

      app.UseRouting();

      app.UseAuthorization();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllerRoute(
                  name: "default",
                  pattern: "{controller=Home}/{action=Index}/{id?}");
      });
    }
  }
}
