# Experimenting with Azure Front Door

## What is the Azure Front Door Service?

> Azure Front Door Service is an Application Delivery Network (ADN) as a service, offering various layer 7 load-balancing capabilities for your applications. It provides dynamic site acceleration (DSA) along with global load balancing with near real-time failover. It is a highly available and scalable service, which is fully managed by Azure.

Ref: <https://docs.microsoft.com/en-us/azure/frontdoor/front-door-faq>

## TL;DR

This code explores the use of Azure Front Door and has resulted in some AFD middleware which only requires a list of front-end hosts and the probe path to work.

``` csharp
    public void ConfigureServices(IServiceCollection services)
    {
      ...

      // Optional: Add Health Checks
      services.AddHealthChecks();

      // Add the Azure Front Door middleware
      services.AddAzureFrontDoor((options) =>
      {
        // Supply a list of AFD front-ends
        options.AllowedFrontEndHosts = new List<string>() { "my.front.door.net" };
        // Path for the AFD health probe
        options.HealthProbePath = "/HealthProbe";
      });

      ...
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IAppRepository appRepository)
    {
      // Optional:Use the Health Checks middleware to provide an endpoint for the AFD probe
      app.UseHealthChecks("/HealthProbe");

      // Use the Azure Front Door middleware
      app.UseAzureFrontDoor();

      ...
    }

```

## So why the experiments?

A: Securing my app behind the Front Door.

Unlike an App Gateway, AFD is a global service with global points of presence which is great, BUT that means my app can be hit from a range of global IP addresses AND (this is the kicker) it could be **ANY** AFD.

In theory, therefore, any AFD gets access to my backend and we don't want this.

The [FAQ](https://docs.microsoft.com/en-us/azure/frontdoor/front-door-faq#how-do-i-lock-down-the-access-to-my-backend-to-only-azure-front-door) suggests:

> To lock down your application to accept traffic only from your specific Front Door, you will need to set up IP ACLs for your backend and then restrict the set of accepted values for the header 'X-Forwarded-Host' sent by Azure Front Door

Once we've set up the IP ACLs we need to address the issue of X-Forwarded-Host; hence the tests and this repo.

NOTE: What the above doesn't mention is the Health Probe.

For general info on working behind a proxy/load balancer see [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer).

## Middleware highlights

The code is built using VS2019 and based on .Net Core 3.1 but I hope the concepts are translatable.

### [AzureFrontDoorExtensions.cs](WTW.CET.AFD.Middleware\AzureFrontDoorExtensions.cs)

This is the entry-point for the code and where you 'Add' the Middleware (usually in Startup.cs: ConfigureServices()) and 'Use' it (usually in Startup.cs: Configure()).

* [Check for a Health Probe](https://docs.microsoft.com/en-us/azure/frontdoor/front-door-health-probes).  See the implementation [here](WTW.CET.AFD.Middleware\AzureFrontDoorMiddleware.cs)
* Process the X-Forwarded headers [using the Forwarded Headers middleware](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
* Use a subclass of Host Filtering Middleware to respond with an Http Status Code of 400 if the resultant host isn't one of our AFD front-ends (see [this discussion on Kestrel](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) for more info about Host Filtering).
  * NOTE: We can't use the standard HF Middleware as this is injected BEFORE anything else.  Host is set to the *back*-end and is therefore failed.

## Health Probes

In order for AFD to determine latency and availability of the backend.

When you design your Front Door you specify the path to your Health Probe -- something that returns a status code of 200 when all's well.

The requests come from the AFD infrastructure so the request passes through the IP ACLs, however, the Health Probe isn't originating from your AFD it's originating from a remote AFD node so there's **no X-Forwarded-Host header** so the protection you've put in place will reject the request and your app will be marked as unhealthy.

Health probe requests do, however, include the header **X-FD-HealthProbe** and set it to **1**.

We therefore have to do some extra header processing to allow for this.

## Ongoing questions

### Q: How can we detect if a **spoof** AFD is probing my app using a 'health probe'?

The Request headers will look legitimate:

* The IP is from an AFD so passes IP restrictions
* X-FD-HealthProbe is set to 1
* X-Forwarded-Host isn't set

A: Ongoing ... any suggestions welcome.

### Q: Are AFD-related X- headers sanitised by AFD?

e.g. Does AFD strip X-FD-HealthProbe from requests going through an AFD?

A: Don't know, will investigate.

### Q: What's the purpose of the backed host header?

When *designing* the AFD there's an option to specify a 'backend host header'.

From [the docs](https://docs.microsoft.com/en-us/azure/frontdoor/front-door-backend-pool):
> Set the backend host header field to a custom value or leave it blank. The hostname for the incoming request will be used as the host header value.

From the blade:
> The host header value sent to the backend with each request. If you leave this blank, the request hostname determines this value. Azure services, such as Web Apps, Blob Storage, and Cloud services, require this host header value to match the target host name by default.

As I'm using a Web App for the backend I guess I can't change it.

## Issues

## Caching caching caching…

Even using the 'Purge' option on AFD I still experienced cached results which makes checking changes difficult.

There's a chance there's a caching issue on my m/c but tried from Incognito, Firefox, IE etc

## Be PATIENT!

Not so much an issue, but calling out a gotcha.  Your AFD changes need to be replicated across the AFD node estate.  This takes time so results of changes to your AFD will not happen instantaneously.

## What next?

Securing services is tricky at best so even if we secure the Web App to only access requests from the AFD service's address range.

* Global nodes ping the Web App legitimately (health probes)
* Global AFD spoofs bypass IP restrictions and hit the Web App
* Global AFD spoofs bypass IP restrictions and can overwhelm the Web App using AFD Health Probes
* Non-obvious code changes need to be made to mitigate the above.

### Suggestions @Microsoft

* Extend the Health Probe process to include an X-Forwarded-Host header set to AFD front-end
  * Access can then follow standard happy path access pattern
* Include Forwarded header processing in the BLADE to reject access to the Web App **before** hitting it
* The Host Filtering middleware for .Net Core rejects access on unrecognised Hosts, however, this isn't re-applied after Forwarded Hosts filtering and there's no option for Forwarded Hosts to reject any hosts not in AllowHosts (via a 400 response for example).  Another approach might be:
  * Apply Forwarded Hosts middleware BEFORE Hosts Filtering
  * Unrecognised Forwarded hosts can then be rejected by the standard Hosts Filtering logic
* Ensure all X-Forwarded-* headers are striped by AFD before injecting its own -- to stop spoofing via AFD
  * TO DO: Check to see if this is done today
* Is there a need for a more secure exchange between AFD and back-end services?
  * Currently we rely solely on the X-Forwarded-Host header.  Is there an opportunity for secret/cert exchange between these services akin to Managed Identities?

## Resolved Issues

Sometimes some magic happens and what you thought was an issue automagically (technical term) resolves itself.

Examples of these can be found [here](readme\Old%20Issues.md).

## Azure DevOps builds

Just for giggles I use DevOps to build this repo and here's its current status:

[![Build Status](https://corp-willistowerswatson.visualstudio.com/CET%20Tools/_apis/build/status/ITCGIO-CET-D-AndyRAFD2-AS%20-%20CI?branchName=master)](https://corp-willistowerswatson.visualstudio.com/CET%20Tools/_build/latest?definitionId=15&branchName=master)
