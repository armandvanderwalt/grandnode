using Grand.Framework.Mvc.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Yotta.GrandNode.Plugins.PayFast
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Success
            routeBuilder.MapRoute("Plugin.Payments.PayFastStandard.Success",
                 "Plugins/PayFastStandard/Success",
                 new { controller = "PayFastStandard", action = "Success" }
            );
            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.PayFastStandard.Cancel",
                "Plugins/PayFastStandard/Cancel",
                new { controller = "PayFastStandard", action = "Cancel" }
            );
            //Notify
            routeBuilder.MapRoute("Plugin.Payments.PayFastStandard.Notify",
                 "Plugins/PayFastStandard/Notify",
                 new { controller = "PayFastStandard", action = "Notify" }
            );
       
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
