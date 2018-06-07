using Microsoft.AspNetCore.Mvc;

namespace Yotta.GrandNode.Plugins.PayFast.Components
{
    [ViewComponent(Name = "PayFastStandard")]
    public class PayFastStandardViewComponent : ViewComponent
    {
        public PayFastStandardViewComponent() { }

        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.PayFastStandard/Views/PayFastStandard/PaymentInfo.cshtml");
        }
    }
}