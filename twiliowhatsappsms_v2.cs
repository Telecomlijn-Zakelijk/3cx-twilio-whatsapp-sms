/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║  3CX Call Processing Script: Twilio SMS Direct Send                      ║
 * ║  Version: 2.0 - Direct SMS with E.164 Twilio Phone Number               ║
 * ║  Company: Telecomlijn Zakelijk B.V.                                     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 *
 * Simplified version: Sends SMS directly from a Twilio E.164 phone number.
 * No IVR/DTMF interaction - caller receives the SMS and the call continues.
 * For landline callers, the SMS step is skipped (no mobile number available).
 */

#nullable disable

using CallFlow;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TCX.Configuration;
using TCX.PBXAPI;

namespace dummy
{
    public class TwilioSmsDirect : ScriptBase<TwilioSmsDirect>
    {
        // ═══════════════════════════════════════════════════════════════════
        // TWILIO CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════

        private const string TwilioAccountSid = "JOUW_TWILIO_ACCOUNT_SID";
        private const string TwilioAuthToken = "JOUW_TWILIO_AUTH_TOKEN";

        // The E.164 phone number you purchased in Twilio (e.g. "+31612345678")
        private const string TwilioSenderPhoneNumber = "+31XXXXXXXXX";

        private const string SmsMessage = "Stel direct uw vraag via WhatsApp: https://jouw-domein.nl/link";

        // ═══════════════════════════════════════════════════════════════════
        // INSTANCE VARIABLES
        // ═══════════════════════════════════════════════════════════════════

        private CancellationTokenSource _cts;
        private bool _callTerminated = false;

        // ═══════════════════════════════════════════════════════════════════
        // MAIN ENTRY POINT
        // ═══════════════════════════════════════════════════════════════════

        public override async void Start()
        {
            await Task.Run(async () =>
            {
                try
                {
                    MyCall.Info("╔═══════════════════════════════════════════════════════════╗");
                    MyCall.Info("║  TwilioSmsDirect v2.0 - Direct SMS Send                   ║");
                    MyCall.Info("╚═══════════════════════════════════════════════════════════╝");

                    _cts = new CancellationTokenSource();
                    MyCall.OnTerminated += OnCallTerminated;

                    string callerNumber = GetOriginalCallerID();
                    MyCall.Info($"Detected Caller ID: {callerNumber ?? "NULL"}");

                    if (string.IsNullOrWhiteSpace(callerNumber))
                    {
                        MyCall.Error("No caller ID available - cannot send SMS");
                        MyCall.Return(false);
                        return;
                    }

                    string e164Number = FormatToE164(callerNumber);
                    MyCall.Info($"E.164 format: {e164Number}");

                    NumberType numberType = ClassifyNumber(e164Number);
                    MyCall.Info($"Number classified as: {numberType}");

                    if (numberType == NumberType.DutchLandline || numberType == NumberType.BelgianLandline)
                    {
                        MyCall.Info("Landline detected - skipping SMS (no mobile number)");
                        MyCall.Return(true);
                        return;
                    }

                    // Send SMS directly - no IVR interaction needed
                    bool sent = await SendTwilioSms(e164Number, SmsMessage);

                    if (sent)
                        MyCall.Info($"SMS sent successfully to {e164Number}");
                    else
                        MyCall.Error($"Failed to send SMS to {e164Number}");

                    MyCall.Info("Script completed successfully");
                }
                catch (OperationCanceledException)
                {
                    MyCall.Info("Call terminated by caller");
                }
                catch (Exception ex)
                {
                    MyCall.Error($"Fatal error: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    MyCall.OnTerminated -= OnCallTerminated;
                    _cts?.Dispose();
                    MyCall.Return(true);
                }
            });
        }

        private void OnCallTerminated()
        {
            _callTerminated = true;
            _cts?.Cancel();
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET ORIGINAL CALLER ID
        // ═══════════════════════════════════════════════════════════════════

        private string GetOriginalCallerID()
        {
            try
            {
                string externalParty = MyCall.ExternalParty;
                if (!string.IsNullOrWhiteSpace(externalParty) && !IsInternalExtension(externalParty))
                    return externalParty;
            }
            catch { }

            try
            {
                var attachedData = MyCall.AttachedData;
                if (attachedData != null)
                {
                    if (attachedData.TryGetValue("extnumber", out string extNumber) &&
                        !string.IsNullOrWhiteSpace(extNumber) && !IsInternalExtension(extNumber))
                        return extNumber;

                    if (attachedData.TryGetValue("public_push_callerid", out string pushCallerId) &&
                        !string.IsNullOrWhiteSpace(pushCallerId) && !IsInternalExtension(pushCallerId))
                        return pushCallerId;
                }
            }
            catch { }

            return MyCall.Caller?.CallerID;
        }

        private bool IsInternalExtension(string number)
        {
            if (string.IsNullOrWhiteSpace(number)) return true;
            if (number.StartsWith("+")) return false;
            if (number.Length <= 4 && int.TryParse(number, out _)) return true;
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // NUMBER CLASSIFICATION
        // ═══════════════════════════════════════════════════════════════════

        private enum NumberType { Unknown, DutchMobile, DutchLandline, BelgianMobile, BelgianLandline, International }

        private NumberType ClassifyNumber(string e164)
        {
            if (string.IsNullOrEmpty(e164)) return NumberType.Unknown;

            if (e164.StartsWith("+31"))
            {
                if (e164.StartsWith("+316") && e164.Length >= 11)
                    return NumberType.DutchMobile;
                return NumberType.DutchLandline;
            }

            if (e164.StartsWith("+32"))
            {
                if (e164.StartsWith("+324") && e164.Length >= 11)
                    return NumberType.BelgianMobile;
                return NumberType.BelgianLandline;
            }

            if (e164.StartsWith("+"))
                return NumberType.International;

            return NumberType.Unknown;
        }

        // ═══════════════════════════════════════════════════════════════════
        // NUMBER FORMATTING
        // ═══════════════════════════════════════════════════════════════════

        private string FormatToE164(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;

            bool hasPlus = phone.StartsWith("+");
            var digits = new StringBuilder();
            foreach (char c in phone)
                if (char.IsDigit(c)) digits.Append(c);
            string d = digits.ToString();

            if (hasPlus) return "+" + d;
            if (d.StartsWith("0") && d.Length >= 10) return "+31" + d.Substring(1);
            if (d.StartsWith("31") && d.Length >= 11) return "+" + d;
            if (d.StartsWith("00") && d.Length >= 12) return "+" + d.Substring(2);
            if (d.Length >= 9) return "+31" + d;
            return "+" + d;
        }

        // ═══════════════════════════════════════════════════════════════════
        // TWILIO SMS
        // ═══════════════════════════════════════════════════════════════════

        private async Task<bool> SendTwilioSms(string toNumber, string message)
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{TwilioAccountSid}:{TwilioAuthToken}"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                    var url = $"https://api.twilio.com/2010-04-01/Accounts/{TwilioAccountSid}/Messages.json";
                    var data = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "To", toNumber },
                        { "From", TwilioSenderPhoneNumber },
                        { "Body", message }
                    });

                    var response = await client.PostAsync(url, data, _cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                MyCall.Error($"Twilio error: {ex.Message}");
                return false;
            }
        }
    }
}
