using Grand.Core.Configuration;

namespace Yotta.GrandNode.Plugins.PayFast
{
    public class PayFastStandardPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public string MerchantId { get; set; }
        public string MerchantKey { get; set; }
        public string Salt { get; set; }
    }
}
