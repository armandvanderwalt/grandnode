using Grand.Core;
using Grand.Core.Domain.Payments;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Grand.Services.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Grand.Core.Domain.Logging;
using Grand.Core.Domain.Orders;
using Yotta.GrandNode.Plugins.PayFast.Models;

namespace Yotta.GrandNode.Plugins.PayFast.Controllers
{

    public class PayFastStandardController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly PayFastStandardPaymentSettings _payFastStandardPaymentSettings;

        public PayFastStandardController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILocalizationService localizationService,
            ILogger logger,
            PayFastStandardPaymentSettings payFastStandardPaymentSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._payFastStandardPaymentSettings = payFastStandardPaymentSettings;
        }

        [AuthorizeAdmin]
        [Area("Admin")]
        public IActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payFastStandardPaymentSettings = _settingService.LoadSetting<PayFastStandardPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = payFastStandardPaymentSettings.UseSandbox,
                MerchantId = payFastStandardPaymentSettings.MerchantId,
                MerchantKey = payFastStandardPaymentSettings.MerchantKey,
                Salt = payFastStandardPaymentSettings.Salt,
                ActiveStoreScopeConfiguration = storeScope
            };
            if (!string.IsNullOrEmpty(storeScope))
            {
                model.UseSandbox_OverrideForStore =
                    _settingService.SettingExists(payFastStandardPaymentSettings, x => x.UseSandbox, storeScope);

                model.MerchantId_OverrideForStore = _settingService.SettingExists(payFastStandardPaymentSettings,
                    x => x.MerchantId, storeScope);

                model.MerchantKey_OverrideForStore = _settingService.SettingExists(payFastStandardPaymentSettings,
                    x => x.MerchantKey, storeScope);

                model.Salt_OverrideForStore = _settingService.SettingExists(payFastStandardPaymentSettings,
                    x => x.Salt, storeScope);
            }

            return View("~/Plugins/Payments.PayFastStandard/Views/PayFastStandard/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area("Admin")]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var patPayFastStandardPaymentSettings = _settingService.LoadSetting<PayFastStandardPaymentSettings>(storeScope);

            //save settings
            patPayFastStandardPaymentSettings.UseSandbox = model.UseSandbox;
            patPayFastStandardPaymentSettings.MerchantId = model.MerchantId;
            patPayFastStandardPaymentSettings.MerchantKey = model.MerchantKey;
            patPayFastStandardPaymentSettings.Salt = model.Salt;
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandbox_OverrideForStore || String.IsNullOrEmpty(storeScope))
                _settingService.SaveSetting(patPayFastStandardPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                _settingService.DeleteSetting(patPayFastStandardPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.MerchantId_OverrideForStore || String.IsNullOrEmpty(storeScope))
                _settingService.SaveSetting(patPayFastStandardPaymentSettings, x => x.MerchantId, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                _settingService.DeleteSetting(patPayFastStandardPaymentSettings, x => x.MerchantId, storeScope);

            if (model.MerchantKey_OverrideForStore || String.IsNullOrEmpty(storeScope))
                _settingService.SaveSetting(patPayFastStandardPaymentSettings, x => x.MerchantKey, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                _settingService.DeleteSetting(patPayFastStandardPaymentSettings, x => x.MerchantKey, storeScope);

            if (model.Salt_OverrideForStore || String.IsNullOrEmpty(storeScope))
                _settingService.SaveSetting(patPayFastStandardPaymentSettings, x => x.Salt, storeScope, false);
            else if (!String.IsNullOrEmpty(storeScope))
                _settingService.DeleteSetting(patPayFastStandardPaymentSettings, x => x.Salt, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        public IActionResult Success(string orderId)
        {
            return RedirectToRoute("CheckoutCompleted", new { orderId = orderId });
        }

        public IActionResult Cancel(string orderId)
        {
            return RedirectToRoute("OrderDetails", new { orderId = orderId });
        }

        public IActionResult Notify(IFormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.PayFastStandard") as PayFastStandardPaymentProcessor;
            
            var queryString  = string.Format("{0}",
                string.Join("&",
                    form.Where(parameter => !parameter.Key.Equals("signature")).Select(kvp => 
                        string.Format("{0}={1}", kvp.Key, HttpUtility.UrlEncode(kvp.Value)))));

            var reg = new Regex(@"%[a-f0-9]{2}");
            var upper = reg.Replace(queryString, m => m.Value.ToUpperInvariant());

            var signature = processor.GenerateMd5Hash(upper);
            var incommingSignature = form["signature"];
            
            var paymentStatus = form["payment_status"];
            var pfPaymentId = form["pf_payment_id"];
            var order = _orderService.GetOrderByGuid(Guid.Parse(form["custom_str1"]));
            var amountPaid = Convert.ToDecimal(form["amount_gross"], CultureInfo.InvariantCulture);
            if (signature.Equals(incommingSignature))
            {
                if (Request.Headers.ContainsKey("Referer"))
                {
                    var referer = Request.Headers["Referer"].ToString();

                    if (referer.Contains("www.payfast.co.za") || referer.Contains("w1w.payfast.co.za") ||
                        referer.Contains("w2w.payfast.co.za") || referer.Contains("sandbox.payfast.co.za"))
                    {
                        if (order.OrderTotal == amountPaid)
                        {
                            if (order.AuthorizationTransactionId == null || !order.AuthorizationTransactionId.Equals(pfPaymentId))
                            {
                                if (paymentStatus.Equals("COMPLETE"))
                                {
                                    try
                                    {

                                        var url = _payFastStandardPaymentSettings.UseSandbox
                                            ? "https://sandbox.payfast.co.za/eng/query/validate"
                                            : "https://www.payfast.co.za/eng/query/validate";

                                        using (var client = new WebClient())
                                        {

                                            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                                            var clientResult = client.UploadString(url, queryString);
                                            if (clientResult.Equals("VALID"))
                                            {
                                                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                                                {
                                                    order.AuthorizationTransactionId = pfPaymentId;
                                                    _orderService.UpdateOrder(order);

                                                    _orderProcessingService.MarkOrderAsPaid(order);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.InsertLog(LogLevel.Error, ex.Message, ex.StackTrace);
                                        return BadRequest();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Ok();
        }
    }
}