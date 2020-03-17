using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WTW.CET.AFD.Middleware
{
  public class AzureFrontDoorOptions
  {
    public IList<string>? AllowedAzureFDIDs
    {
      get
      {
      }

      set
      {
      }
    }
    public AzureFrontDoorOptions()
    {
    }

    public AzureFrontDoorOptions(Action<AzureFrontDoorOptions> configureOptions) : this()
    {
      configureOptions(this);
      Validate();
    }

    internal void Validate()
    {
      if (AllowedAzureFDIDs == null) throw new ArgumentNullException(nameof(AllowedAzureFDIDs));
    }
  }
}