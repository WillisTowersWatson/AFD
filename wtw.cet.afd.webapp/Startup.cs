using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using wtw.cet.afd.webapp.Interfaces;

namespace wtw.cet.afd.webapp
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      services.AddControllersWithViews();

      /////////////////////////////////////////////////

      // Create and register our app repo singleton
      var appRepository = new AppRepository();
      {
        services.AddSingleton<IAppRepository>(appRepository);
        appRepository.AllowedHosts = Configuration.GetValue<string>("AllowedHosts")?.Split(';').ToList();
        appRepository.AllowedForwardedHosts = Configuration.GetValue<string>("AllowedForwardedHosts")?.Split(';').ToList();
      }

      // enable access to the HttpContext for display purposes
      services.AddHttpContextAccessor();

      // Configure the ForwardedHeaders processing
      services.Configure<ForwardedHeadersOptions>(options =>
      {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        // Restrict which forwarding hosts we will allow
        options.AllowedHosts = appRepository.AllowedForwardedHosts;
      });

      // add health checking
      services.AddHealthChecks();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      // process X-Forwarded* headers
      app.UseForwardedHeaders();

      // Use health checks
      app.UseHealthChecks("/healthcheck");

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
