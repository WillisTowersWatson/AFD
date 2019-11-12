# Old Issues

## Over probing

When we specify the AFD back end, we can specity both the path to the probe, the protocol (HTTP/HTTPS) and the *Probe Interval (seconds)*.

If our app wants a quiet life we set the probe interval to 255 seconds so we get probed once every 4 or so minutes right?

**NOPE!**

Because it's a global service with many nodes and the last time I checked my app gets pinged **200 times/minute**!  My simple maths indicates that **850 nodes** are now hammering my app :(

### Currently (12 Nov '19)

![Bouncing Health Probe](Happy%20Probing.PNG?raw=true "Happy Health Probe")

This shows the app is being accessed about 25 times/minute so as the probe is set to 255 second delay this equates to approximately 100 AFD probing nodes.

* Interval: x seconds
* Single Node rate/m: 60/x
* Actual: 25/m
* \# probing Nodes: Actual / SingleNodeRate = ~100.

This seems a much more reasonable rate and # nodes.

NOTE: I'll be resetting the interval back to 30 seconds as this is is the MSFT recommended interval.

According to the above and my findings from the above U should then expect a request rate of ~200/m.

## Change your probe path at your peril

![Bouncing Health Probe](Bouncing%20Probe%20Health.PNG?raw=true "Bouncing Health Probe")

* The above chart shows 2 probes ... I only have 1 configured (the dark blue 'line' in this)
* Something odd is going on with the latest probe.  Perhaps a set of the AFD nodes are still pinging my old (and now invalid) health probe.

### Currently (12 Nov '19)

During my tests I had created a 'spoof' AFD and I suspect this was running and probing the same App Service.

I wonder if this is how the AFD reports on the probe's health.

TO DO: Test this hypothesis
