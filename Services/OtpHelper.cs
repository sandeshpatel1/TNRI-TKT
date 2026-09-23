using System.Security.Cryptography;
using TanaririTickets.Models;

namespace TanaririTickets.Services;

public static class OtpHelper
{
    /// <summary>True when no SMS gateway is configured and the entered code matches the dev bypass code.
    /// Automatically stops working the moment Sms:UrlTemplate is filled in.</summary>
    public static bool IsDevBypass(string entered, SmsOptions sms) =>
        string.IsNullOrWhiteSpace(sms.UrlTemplate) &&
        !string.IsNullOrWhiteSpace(sms.DevBypassOtp) &&
        entered == sms.DevBypassOtp;

    public static string NewOtp() => RandomNumberGenerator.GetInt32(1000, 10000).ToString();

    public static string Mask(string mobile) =>
        mobile.Length >= 10 ? new string('X', 6) + mobile[^4..] : mobile;

    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}