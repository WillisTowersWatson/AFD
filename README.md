# Experimenting with Azure Front Door

## What is Azure Front Door Service?

Azure Front Door Service is an Application Delivery Network (ADN) as a service, offering various layer 7 load-balancing capabilities for your applications. It provides dynamic site acceleration (DSA) along with global load balancing with near real-time failover. It is a highly available and scalable service, which is fully managed by Azure.

Ref: <https://docs.microsoft.com/en-us/azure/frontdoor/front-door-faq>

## So why the experiments?

A: Securing my app behind the Front Door.

Unlike an App Gateway, AFD is a global service with global points of presence which is great, BUT ... that means my app can be hit from a range of global IP addresses AND (this is the kicker) it could be **ANY** AFD.

In theory, therefore, any AFD gets access to my backend and we don't want this.

The [FAQ](https://docs.microsoft.com/en-us/azure/frontdoor/front-door-faq#how-do-i-lock-down-the-access-to-my-backend-to-only-azure-front-door) suggests:

> To lock down your application to accept traffic only from your specific Front Door, you will need to set up IP ACLs for your backend and then restrict the set of accepted values for the header 'X-Forwarded-Host' sent by Azure Front Door

Once we've set up the IP ACLs we need to address the issue of X-Forwarded-Host hence the tests and simple app.

NOTE: What the above it doesn't mention is the Health Probe.

## Health Probes

In order for AFD to determine latency and availability of the backend.

When you design your Front Door you specify the URL of your Health Probe -- something that returns a status code of 200 when all's well.

The requests come from the AFD infrastructure so the request passes through the IP ACLs, however, the Health Probe isn't originating from your AFD it's originating from a remote AFD probe so there's **no X-Forwarded-Host header** do the protection you've put in place will reject the request and your app will be marked as unhealthy.

Health probe requests do, however, include the header **X-FD-HealthProbe** and set it to **1**.

We therefore need to do some more header processing to allow for this.

### Q: How can we detect if a **spoof** AFD is probing my app using a 'health probe'?

The Request headers will look legitimate:

* The IP is from an AFD so passes IP restrictions
* X-FD-HealthProbe is set to 1
* X-Forwarded-Host isn't set

### Q: How to secure site while allowing health probe?

Without any other remediation steps:

* Host filter (AllowHosts -- NOT the Forwarded Header AllowHosts option) have to include the backend azurewebsites.net FQDN as well as the AFD FQDN

### Q: Are AFD-related X- headers sanitised by AFD?

e.g. Does AFD strip X-FD-HealthProbe from requests going through an AFD?

A: Good question, don't know

## Issues

### Over probing

When we specify the AFD back end, we can specity both the path to the probe, the protocol (HTTP/HTTPS) and the *Probe Interval (seconds)*.

If out app wants a quiet life we set the probe interval to 255 seconds so we get probed once every 4 or so minutes right?

**NOPE!**

Because it's a global service with many endpoints the last time I checked my app gets pinged **200 times/minute**!  My simple maths indicates that **850 sources** are now hammering my app :(

## Caching caching caching…

Even using the 'Purge' option on AFD I still experienced cached results which makes checking changes difficult.

There's a chance there's a caching issue on my m/c but tried from Incognito, Firefox, IE etc

## Change your probe path at your peril

![Bouncing Health Probe](readme/Bouncing%20Probe%20Health.png "Bouncing Health Probe")

Looks like at least one of the AFD probes is still pinging my old (and now invalid) health probe.

## Azure DevOps builds

Just for giggles I use DevOps to build this repo and here's its current status:

[![Build Status](https://corp-willistowerswatson.visualstudio.com/CET%20Tools/_apis/build/status/ITCGIO-CET-D-AndyRAFD2-AS%20-%20CI?branchName=master)](https://corp-willistowerswatson.visualstudio.com/CET%20Tools/_build/latest?definitionId=15&branchName=master)