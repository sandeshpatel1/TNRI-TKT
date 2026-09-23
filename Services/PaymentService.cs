using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TanaririTickets.Models;

namespace TanaririTickets.Services;

/// <summary>Easebuzz integration (initiate link + transaction retrieve).</summary>
public class PaymentService
{
    private readonly EasebuzzOptions _o;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PaymentService> _log;

    public PaymentService(IOptions<EasebuzzOptions> o, IHttpClientFactory http, ILogger<PaymentService> log)
    { _o = o.Value; _http = http; _log = log; }

    public static string Amt(decimal a) => a.ToString("F1", CultureInfo.InvariantCulture);

    private static string Sha512(string s) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    /// <returns>(redirectUrl, error)</returns>
    public async Task<(string? Url, string? Error)> InitiateAsync(Booking b, string successUrl, string failUrl)
    {
        if (string.IsNullOrEmpty(_o.Key) || string.IsNullOrEmpty(_o.Salt))
            return (null, "Payment gateway is not configured yet.");

        var txnid = b.BookingId.ToString();
        var amount = Amt(b.TotalAmount);
        const string productInfo = "Tanariri Musical Museum Tickets";
        var name = b.VisitorName ?? "";
        var email = b.Email ?? "";

        // key|txnid|amount|productinfo|firstname|email|udf1..udf10|salt
        var parts = new[] { _o.Key, txnid, amount, productInfo, name, email }
            .Concat(Enumerable.Repeat("", 10))          // udf1..udf10
            .Append(_o.Salt);
        var hash = Sha512(string.Join("|", parts));

        var form = new Dictionary<string, string>
        {
            ["key"] = _o.Key, ["txnid"] = txnid, ["amount"] = amount, ["productinfo"] = productInfo,
            ["firstname"] = name, ["phone"] = b.Mobile, ["email"] = email,
            ["surl"] = successUrl, ["furl"] = failUrl, ["hash"] = hash
        };

        try
        {
            var res = await _http.CreateClient().PostAsync(_o.InitiateUrl, new FormUrlEncodedContent(form));
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var st) && st.GetInt32() == 1)
                return (_o.PayBaseUrl + root.GetProperty("data").GetString(), null);

            _log.LogError("Easebuzz initiate failed: {Body}", root.ToString());
            return (null, "Could not start the payment. Please try again.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Easebuzz initiate error");
            return (null, "Payment service is unavailable. Please try again.");
        }
    }

    /// <summary>Asks Easebuzz for the authoritative status of a transaction.</summary>
    public async Task<PaymentResult?> RetrieveAsync(Booking b)
    {
        try
        {
            var amount = Amt(b.TotalAmount);
            // key|txnid|amount|email|phone|salt
            var hash = Sha512($"{_o.Key}|{b.BookingId}|{amount}|{b.Email}|{b.Mobile}|{_o.Salt}");
            var payload = JsonSerializer.Serialize(new
            {
                txnid = b.BookingId.ToString(), key = _o.Key, amount, email = b.Email, phone = b.Mobile, hash
            });

            var res = await _http.CreateClient().PostAsync(_o.RetrieveUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"));
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            if (!doc.RootElement.TryGetProperty("msg", out var msg) || msg.ValueKind != JsonValueKind.Object) return null;

            string S(string n) => msg.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
            decimal.TryParse(S("amount"), NumberStyles.Any, CultureInfo.InvariantCulture, out var paid);
            return new PaymentResult(S("status"), S("mode"), S("easepayid"), S("bank_ref_num"), paid);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Easebuzz retrieve error");
            return null;
        }
    }
}
