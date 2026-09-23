using Microsoft.Extensions.Options;
using TanaririTickets.Models;

namespace TanaririTickets.Services;

public class SmsService
{
    private readonly SmsOptions _o;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<SmsService> _log;

    public SmsService(IOptions<SmsOptions> o, IHttpClientFactory http, ILogger<SmsService> log)
    { _o = o.Value; _http = http; _log = log; }

    public async Task SendOtpAsync(string mobile, string otp)
    {
        if (string.IsNullOrWhiteSpace(_o.UrlTemplate))
        {
            // No SMS provider configured yet - log so you can test locally.
            _log.LogWarning("SMS provider not configured. OTP for {Mobile} is {Otp}", mobile, otp);
            return;
        }

        var msg = _o.MessageTemplate.Replace("{otp}", otp);
        var url = _o.UrlTemplate
            .Replace("{mobile}", Uri.EscapeDataString(mobile))
            .Replace("{otp}", Uri.EscapeDataString(otp))
            .Replace("{message}", Uri.EscapeDataString(msg));
        try
        {
            var res = await _http.CreateClient().GetAsync(url);
            if (!res.IsSuccessStatusCode) _log.LogError("SMS gateway returned {Code}", res.StatusCode);
        }
        catch (Exception ex) { _log.LogError(ex, "SMS send failed"); }
    }
}
