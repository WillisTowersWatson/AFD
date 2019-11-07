using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using WTW.CET.AFD.Middleware;
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
        string getConfigValue(string appSettingName)
        {
          var result = Configuration.GetValue<string>(appSettingName);
          if (result == null) throw new ArgumentNullException(appSettingName, $"Application setting missing");
          return result;
        }

        IList<string> getList(string appSettingName)
        {
          return getConfigValue(appSettingName).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        appRepository.AllowedFrontEndHosts = getList("AFD.AllowedFrontEndHosts"); 
        appRepository.HealthProbePath = getConfigValue("AFD.HealthProbePath");

        services.AddSingleton<IAppRepository>(appRepository);
      }

      ///////////////////////////////////////////////
      /// Specific AFD processing
      ///////////////////////////////////////////////

      // add health checking
      services.AddHealthChecks();

      services.AddAzureFrontDoor((options) =>
      {
        options.AllowedFrontEndHosts = appRepository.AllowedFrontEndHosts;
        options.HealthProbePath = appRepository.HealthProbePath;
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reflection will not allow this to be static")]
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IAppRepository appRepository)
    {
      //try
      //{
      //  // todo: so we can step into the Host Filtering middleware
      //  var x = new HostFilteringMiddleware(null, null, null);
      //}
      //catch (Exception)
      //{

      //}

      app.Use(async (context, next) =>
      {
        appRepository.HttpContexts.Add(new HttpContextCacheEntry(context));
        await next();
      });

      // Use health checks
      app.UseHealthChecks(appRepository.HealthProbePath);

      app.UseAzureFrontDoor();

      app.Use(async (context, next) =>
      {
        appRepository.HttpContexts.Add(new HttpContextCacheEntry(context));
        await next();
      });

      //////////////////////////////////////////////////////

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
