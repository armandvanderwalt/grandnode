using Grand.Framework.Mvc.ModelBinding;
using Grand.Framework.Mvc.Models;

namespace Yotta.GrandNode.Plugins.PayFast.Models
{
    public class ConfigurationModel : BaseGrandModel
    {
        
        public string ActiveStoreScopeConfiguration { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.PayFastStandard.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.PayFastStandard.Fields.MerchantId")]
        public string MerchantId { get; set; }
        public bool MerchantId_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.PayFastStandard.Fields.MerchantKey")]
        public string MerchantKey { get; set; }
        public bool MerchantKey_OverrideForStore { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.PayFastStandard.Fields.Salt")]
        public string Salt { get; set; }
        public bool Salt_OverrideForStore;
    }
}
