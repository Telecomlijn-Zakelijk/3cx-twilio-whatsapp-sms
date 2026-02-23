/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║  3CX Call Processing Script: Twilio SMS Direct Send                      ║
 * ║  Version: 2.0 - SMS with E.164 Twilio Phone Number (+31)                ║
 * ║  Company: Telecomlijn Zakelijk B.V.                                     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 *
 * Sends SMS from a Twilio E.164 phone number (e.g. +31612345678) instead of
 * an alphanumeric sender name. Full IVR/DTMF interaction for landline callers.
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
        // TIMING CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════

        private const int MaxRetries = 3;
        private const int InputTimeoutMs = 30000;        // 30 sec total timeout
        private const int InterDigitTimeoutMs = 5000;    // 5 sec between digits
        private const int ConfirmationTimeoutMs = 10000; // 10 sec to press 1 or 2

        // ═══════════════════════════════════════════════════════════════════
        // AUDIO FILE PATHS
        // ═══════════════════════════════════════════════════════════════════

        private const string AudioFolder = "Callflows/twiliowhatsappsms/";

        private static readonly string AudioSmsPrompt = AudioFolder + "sms_whatsapp_prompt.wav";
        private static readonly string AudioLandlineDetected = AudioFolder + "landline_detected.wav";
        private static readonly string AudioEnterMobile = AudioFolder + "enter_mobile.wav";
        private static readonly string AudioYouEntered = AudioFolder + "you_entered.wav";
        private static readonly string AudioIsThisCorrect = AudioFolder + "is_this_correct.wav";
        private static readonly string AudioInvalidNumber = AudioFolder + "invalid_number.wav";
        private static readonly string AudioSmsSent = AudioFolder + "sms_sent.wav";
        private static readonly string AudioNoInput = AudioFolder + "no_input.wav";
        private static readonly string AudioGoodbye = AudioFolder + "goodbye.wav";

        private static readonly string[] DigitAudio = new string[]
        {
            AudioFolder + "digit_0.wav",
            AudioFolder + "digit_1.wav",
            AudioFolder + "digit_2.wav",
            AudioFolder + "digit_3.wav",
            AudioFolder + "digit_4.wav",
            AudioFolder + "digit_5.wav",
            AudioFolder + "digit_6.wav",
            AudioFolder + "digit_7.wav",
            AudioFolder + "digit_8.wav",
            AudioFolder + "digit_9.wav"
        };

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
                    MyCall.Info("║  TwilioSmsDirect v2.0 - E.164 Phone Number Sender        ║");
                    MyCall.Info("╚═══════════════════════════════════════════════════════════╝");

                    _cts = new CancellationTokenSource();
                    MyCall.OnTerminated += OnCallTerminated;

                    string callerNumber = GetOriginalCallerID();
                    MyCall.Info($"Detected Caller ID: {callerNumber ?? "NULL"}");

                    if (string.IsNullOrWhiteSpace(callerNumber))
                    {
                        MyCall.Error("No caller ID available");
                        MyCall.Return(false);
                        return;
                    }

                    string e164Number = FormatToE164(callerNumber);
                    MyCall.Info($"E.164 format: {e164Number}");

                    await MyCall.AssureMedia();
                    MyCall.Info("Media channel established");

                    NumberType numberType = ClassifyNumber(e164Number);
                    MyCall.Info($"Number classified as: {numberType}");

                    string targetNumber;

                    switch (numberType)
                    {
                        case NumberType.DutchMobile:
                        case NumberType.BelgianMobile:
                        case NumberType.International:
                            targetNumber = e164Number;
                            await ProcessMobileFlow(targetNumber);
                            break;

                        case NumberType.DutchLandline:
                        case NumberType.BelgianLandline:
                            targetNumber = await ProcessLandlineFlow();
                            if (!string.IsNullOrEmpty(targetNumber))
                            {
                                await SendSmsAndConfirm(targetNumber);
                            }
                            break;

                        default:
                            targetNumber = e164Number;
                            await ProcessMobileFlow(targetNumber);
                            break;
                    }

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
        // FLOW: MOBILE CALLER
        // ═══════════════════════════════════════════════════════════════════

        private async Task ProcessMobileFlow(string mobileNumber)
        {
            MyCall.Info($"Processing MOBILE flow for: {mobileNumber}");
            await PlayAudio(AudioSmsPrompt);
            await SendSmsAndConfirm(mobileNumber);
        }

        // ═══════════════════════════════════════════════════════════════════
        // FLOW: LANDLINE CALLER
        // ═══════════════════════════════════════════════════════════════════

        private async Task<string> ProcessLandlineFlow()
        {
            MyCall.Info("Processing LANDLINE flow");

            await PlayAudio(AudioLandlineDetected);

            int attempts = 0;

            while (attempts < MaxRetries && !_callTerminated)
            {
                attempts++;
                MyCall.Info($"Attempt {attempts}/{MaxRetries}");

                // Collect phone number (expects 10 digits or #)
                string enteredNumber = await GetMultiDigitInput(AudioEnterMobile, 10, InterDigitTimeoutMs, InputTimeoutMs);

                if (_callTerminated) return null;

                if (string.IsNullOrEmpty(enteredNumber))
                {
                    MyCall.Info("No input received");
                    await PlayAudio(AudioNoInput);
                    if (attempts >= MaxRetries)
                    {
                        await PlayAudio(AudioGoodbye);
                        return null;
                    }
                    continue;
                }

                MyCall.Info($"User entered: {enteredNumber}");

                if (!IsValidMobileNumber(enteredNumber))
                {
                    MyCall.Info($"Invalid number: {enteredNumber}");
                    await PlayAudio(AudioInvalidNumber);
                    if (attempts >= MaxRetries)
                    {
                        await PlayAudio(AudioGoodbye);
                        return null;
                    }
                    continue;
                }

                // Play back the number - ALL DIGITS IN ONE CALL (fast!)
                MyCall.Info("Playing back number for confirmation");
                await PlayNumberReadback(enteredNumber);

                // Get single digit confirmation (1 or 2) - INSTANT response
                string confirmation = await GetSingleDigitInput(AudioIsThisCorrect, ConfirmationTimeoutMs);

                if (_callTerminated) return null;

                MyCall.Info($"Confirmation input: '{confirmation}'");

                if (confirmation == "1" || string.IsNullOrEmpty(confirmation))
                {
                    MyCall.Info("Number confirmed");
                    return FormatMobileToE164(enteredNumber);
                }
                else if (confirmation == "2")
                {
                    MyCall.Info("User wants to re-enter");
                    // Loop continues
                }
                else
                {
                    MyCall.Info("Unexpected input, treating as confirm");
                    return FormatMobileToE164(enteredNumber);
                }
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // DTMF: MULTI-DIGIT INPUT (for phone number)
        // ═══════════════════════════════════════════════════════════════════

        private async Task<string> GetMultiDigitInput(string promptFile, int maxDigits, int interDigitTimeout, int totalTimeout)
        {
            MyCall.Info($"GetMultiDigitInput: max={maxDigits} digits");

            string inputBuffer = "";
            var tcs = new TaskCompletionSource<string>();

            var timer = new Timer(_ => tcs.TrySetResult(inputBuffer), null, totalTimeout, Timeout.Infinite);

            Action<char> handler = (char c) =>
            {
                MyCall.Info($"DTMF: '{c}'");

                if (c == '#')
                {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    tcs.TrySetResult(inputBuffer);
                }
                else if (c == '*')
                {
                    inputBuffer = "";
                    timer.Change(interDigitTimeout, Timeout.Infinite);
                }
                else if (char.IsDigit(c))
                {
                    inputBuffer += c;
                    timer.Change(interDigitTimeout, Timeout.Infinite);

                    if (inputBuffer.Length >= maxDigits)
                    {
                        timer.Change(Timeout.Infinite, Timeout.Infinite);
                        tcs.TrySetResult(inputBuffer);
                    }
                }
            };

            MyCall.OnDTMFInput += handler;

            try
            {
                await MyCall.PlayPrompt(null, new[] { promptFile },
                    PlayPromptOptions.ResetBufferAtStart | PlayPromptOptions.CancelPlaybackAtFirstChar);

                return await tcs.Task;
            }
            finally
            {
                timer.Dispose();
                MyCall.OnDTMFInput -= handler;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // DTMF: SINGLE DIGIT INPUT (for confirmation - INSTANT response)
        // ═══════════════════════════════════════════════════════════════════

        private async Task<string> GetSingleDigitInput(string promptFile, int timeout)
        {
            MyCall.Info("GetSingleDigitInput: waiting for 1 digit");

            string inputBuffer = "";
            var tcs = new TaskCompletionSource<string>();

            var timer = new Timer(_ => tcs.TrySetResult(inputBuffer), null, timeout, Timeout.Infinite);

            Action<char> handler = (char c) =>
            {
                MyCall.Info($"DTMF: '{c}'");

                if (char.IsDigit(c))
                {
                    inputBuffer = c.ToString();
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    // INSTANT completion on first digit!
                    tcs.TrySetResult(inputBuffer);
                }
            };

            MyCall.OnDTMFInput += handler;

            try
            {
                await MyCall.PlayPrompt(null, new[] { promptFile },
                    PlayPromptOptions.ResetBufferAtStart | PlayPromptOptions.CancelPlaybackAtFirstChar);

                return await tcs.Task;
            }
            finally
            {
                timer.Dispose();
                MyCall.OnDTMFInput -= handler;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PLAY NUMBER READBACK - ALL DIGITS IN ONE CALL (fast!)
        // ═══════════════════════════════════════════════════════════════════

        private async Task PlayNumberReadback(string number)
        {
            if (_callTerminated) return;

            // Build array of all audio files to play in sequence
            var audioFiles = new List<string>();
            audioFiles.Add(AudioYouEntered);  // "U heeft ingevoerd:"

            foreach (char digit in number)
            {
                if (char.IsDigit(digit))
                {
                    int index = digit - '0';
                    audioFiles.Add(DigitAudio[index]);
                }
            }

            MyCall.Info($"Playing {audioFiles.Count} audio files in one call");

            try
            {
                // Play ALL files in ONE PlayPrompt call - much faster!
                await MyCall.PlayPrompt(null, audioFiles.ToArray(), PlayPromptOptions.Blocked);
            }
            catch (Exception ex)
            {
                MyCall.Error($"Readback error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // SEND SMS AND CONFIRM
        // ═══════════════════════════════════════════════════════════════════

        private async Task SendSmsAndConfirm(string targetNumber)
        {
            MyCall.Info($"Sending SMS to: {targetNumber}");

            bool sent = await SendTwilioSms(targetNumber, SmsMessage);

            if (sent)
            {
                MyCall.Info("SMS sent successfully");
                await PlayAudio(AudioSmsSent);
            }
            else
            {
                MyCall.Error("SMS failed");
            }

            await PlayAudio(AudioGoodbye);
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
        // NUMBER VALIDATION & FORMATTING
        // ═══════════════════════════════════════════════════════════════════

        private bool IsValidMobileNumber(string input)
        {
            string cleaned = input.Replace(" ", "").Replace("-", "");

            foreach (char c in cleaned)
                if (!char.IsDigit(c)) return false;

            // Dutch mobile: 06xxxxxxxx (10 digits) or 6xxxxxxxx (9 digits)
            if (cleaned.StartsWith("06") && cleaned.Length == 10)
                return true;
            if (cleaned.StartsWith("6") && cleaned.Length == 9)
                return true;

            // Belgian mobile: 04xxxxxxxx (10 digits) or 4xxxxxxxx (9 digits)
            if (cleaned.StartsWith("04") && cleaned.Length == 10)
                return true;
            if (cleaned.StartsWith("4") && cleaned.Length == 9)
                return true;

            return false;
        }

        private string FormatMobileToE164(string input)
        {
            string cleaned = input.Replace(" ", "").Replace("-", "");

            // Belgian mobile: 04xx → +324xx
            if (cleaned.StartsWith("04")) return "+32" + cleaned.Substring(1);
            if (cleaned.StartsWith("4")) return "+32" + cleaned;

            // Dutch mobile: 06xx → +316xx
            if (cleaned.StartsWith("06")) return "+31" + cleaned.Substring(1);
            if (cleaned.StartsWith("6")) return "+31" + cleaned;

            return "+31" + cleaned;
        }

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

        // ═══════════════════════════════════════════════════════════════════
        // AUDIO PLAYBACK
        // ═══════════════════════════════════════════════════════════════════

        private async Task PlayAudio(string path)
        {
            if (_callTerminated) return;
            try
            {
                await MyCall.PlayPrompt(null, new[] { path }, PlayPromptOptions.Blocked);
            }
            catch (Exception ex)
            {
                MyCall.Error($"Audio error: {ex.Message}");
            }
        }
    }
}
