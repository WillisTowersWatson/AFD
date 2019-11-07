// #define IncludeToddIssues

using System.Collections;
using System.Collections.Generic;
using System.Net;

namespace WTW.CET.AFD.Tests
{
  internal class XForwardedForTestData : IEnumerable<object?[]>
  {
    public static readonly List<string> AllowedFrontEndHosts = new List<string>() { "my.front.door.net" };
    public static readonly List<string> AllowedHosts = new List<string>() { "my.front.door.net", "my.back.end.net" };
    public const string HealthProbePath = "/HealthProbe";

    public const string BAD_HEALTH_PROBE_PATH = "/PokeTheApp";

    public IEnumerator<object?[]> GetEnumerator()
    {
      // happy path
      yield return new object?[] { "MY.Front.Door.net", "MY.Front.Door.net", null, HttpStatusCode.OK };

      // our AFD is probing (requests by the AFD probing mechanism)
      // TODO: How can we check for other AFDs probing us?
      yield return new object?[] { null, "my.azurewebsite.net", HealthProbePath, HttpStatusCode.OK };

      // our AFD is probing the wrong path!
      yield return new object?[] { null, "my.azurewebsite.net", BAD_HEALTH_PROBE_PATH, HttpStatusCode.BadRequest };

      // someone is using another AFD to access the backend
      yield return new object?[] { "spoof.front.door.net", "my.azurewebsite.net", null, HttpStatusCode.BadRequest };

      // someone is using our AFD to spoof a probe by injecting X-FD-HealthProbe headers
      // TODO: Check to see if AFD strips this from headers before passing it on
      yield return new object?[] { "my.front.door.net", "MY.Front.Door.net", HealthProbePath, HttpStatusCode.BadRequest };

      // someone is using our AFD to spoof a probe by injecting X-FD-HealthProbe headers
      // TODO: Check to see if AFD strips this from headers before passing it on
      yield return new object?[] { "my.front.door.net", "MY.Front.Door.net", BAD_HEALTH_PROBE_PATH, HttpStatusCode.BadRequest };

      // a spoof proxy is being used to probe (requests via their AFD for example)
      yield return new object?[] { "SPOOF.Front.Door.net", "my.azurewebsite.net", HealthProbePath, HttpStatusCode.BadRequest };

      // a hacker is using their probe as a way of accessing/doing a ddos attack on a poorly secured app which may allow any probes through
      yield return new object?[] { "SPOOF.Front.Door.net", "my.azurewebsite.net", BAD_HEALTH_PROBE_PATH, HttpStatusCode.BadRequest };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}