using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Grand.Core;
using Grand.Core.Domain.Logging;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Shipping;
using Grand.Core.Infrastructure;
using Grand.Core.Plugins;
using Grand.Services.Catalog;
using Grand.Services.Configuration;
using Grand.Services.Directory;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Yotta.GrandNode.Plugins.PayFast.Controllers;

namespace Yotta.GrandNode.Plugins.PayFast
{
    public class PayFastStandardPaymentProcessor : BasePlugin, IPaymentMethod
    {
        private readonly IWebHelper _webHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly ISettingService _settingService;
        private readonly IProductService _productService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        private readonly PayFastStandardPaymentSettings _payFastStandardPaymentSettings;

        public PayFastStandardPaymentProcessor(IWebHelper webHelper, IHttpContextAccessor httpContextAccessor, ILocalizationService localizationService, ISettingService settingService, ILogger logger, IProductService productService, PayFastStandardPaymentSettings payFastStandardPaymentSettings, IOrderTotalCalculationService orderTotalCalculationService)
        {
            _webHelper = webHelper;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _settingService = settingService;
            _payFastStandardPaymentSettings = payFastStandardPaymentSettings;
            _orderTotalCalculationService = orderTotalCalculationService;
            _productService = productService;
            _logger = logger;
        }

        #region Helpers

        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created query parameters</returns>
        private IDictionary<string, string> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();


            var itemName = new StringBuilder();

            foreach (var orderOrderItem in postProcessPaymentRequest.Order.OrderItems)
            {
                var product = _productService.GetProductById(orderOrderItem.ProductId);
                itemName.AppendFormat("{0}, ",product.Name);
            }
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            //create query parameters
            return new Dictionary<string, string>
            {
                ["merchant_id"] = _payFastStandardPaymentSettings.MerchantId,
                ["merchant_key"] = _payFastStandardPaymentSettings.MerchantKey,
                ["return_url"] = $"{storeLocation}Plugins/PayFastStandard/Success?orderId=" + postProcessPaymentRequest.Order.Id,
                ["cancel_url"] = $"{storeLocation}Plugins/PayFastStandard/Cancel?orderId=" + postProcessPaymentRequest.Order.Id,
                ["notify_url"] = $"{storeLocation}Plugins/PayFastStandard/Notify",

                //buyer details
                ["amount"] = roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture),
                ["item_name"] = itemName.ToString().Trim(),


                //order identifier
                ["custom_str1"] = postProcessPaymentRequest.Order.OrderGuid.ToString()
            };
        }


        /// <summary>
        /// Gets PayPal URL
        /// </summary>
        /// <returns></returns>
        private string GetPayFastUrl()
        {
            return _payFastStandardPaymentSettings.UseSandbox ?
                "https://sandbox.payfast.co.za/eng/process" :
                "https://www.payfast.co.za/eng/process";
        }

        public string GenerateMd5Hash(string queryParameters)
        {
            if (!string.IsNullOrEmpty(_payFastStandardPaymentSettings.Salt))
            {
                queryParameters += "&passphrase=" + _payFastStandardPaymentSettings.Salt;
            }

            var asciiBytes = ASCIIEncoding.ASCII.GetBytes(queryParameters);
            var hashedBytes = MD5CryptoServiceProvider.Create().ComputeHash(asciiBytes);
            var hashedString = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            return hashedString;
        }


        #endregion Helpers

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //create common query parameters for the request
            var queryParameters = CreateQueryParameters(postProcessPaymentRequest);

            //remove null values from parameters
            queryParameters = queryParameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

            var query = string.Format("{0}",
                string.Join("&",
                    queryParameters.Select(kvp =>
                        string.Format("{0}={1}", kvp.Key, HttpUtility.UrlEncode(kvp.Value)))));
            var reg = new Regex(@"%[a-f0-9]{2}");
            var upper = reg.Replace(query, m => m.Value.ToUpperInvariant());

            reg = new Regex(@"%20");
            upper = reg.Replace(upper, "+");

            var signature = GenerateMd5Hash(upper);

           

            queryParameters.Add("signature", signature);

            var url = QueryHelpers.AddQueryString(GetPayFastUrl(), queryParameters);
            _httpContextAccessor.HttpContext.Response.Redirect(url);
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0.0M;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 20)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PayFastStandard/Configure";
        }

        public Type GetControllerType()
        {
            return typeof(PayFastStandardController);
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PayFastStandard";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PayFastStandardPaymentSettings
            {
                UseSandbox = true
            });

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantId.Hint", "Enter Merchant ID provided by PayFast.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantKey", "Merchant Key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantKey.Hint", "Merchant Key provided by PayFast.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.Salt", "MD5 Salt");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.Salt.Hint", "Salt used for MD5");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.PaymentMethodDescription", "You will be redirected to PayFast to complete the payment");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.RedirectionTip", "You will be redirected to PayFast to complete the order.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayFastStandardPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantKey");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.MerchantKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.PaymentMethodDescription");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.Salt");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFastStandard.Fields.Salt.Hint");


            base.Uninstall();
        }



        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.PayFastStandard.PaymentMethodDescription"); }
        }

        #endregion
    }
}