namespace TanaririTickets.Models;

public class MuseumOptions
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Hours { get; set; } = "";
    public string HomeUrl { get; set; } = "/";
    public string PrivacyUrl { get; set; } = "#";
    public string TermsUrl { get; set; } = "#";
}

public class TicketCategory
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Audience { get; set; } = "Indian";   // Indian | Foreigner
}

public class BookingOptions
{
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public int SessionMinutes { get; set; } = 10;
    public int OtpMinutes { get; set; } = 5;
    public int MaxOtpAttempts { get; set; } = 5;
    public string SlotStart { get; set; } = "10:00";
    public string SlotEnd { get; set; } = "18:50";
    public int SlotIntervalMinutes { get; set; } = 10;
    public int SlotCapacity { get; set; } = 8;
    public int LeadMinutes { get; set; } = 60;
    public int AdvanceDays { get; set; } = 60;
    public int PendingHoldMinutes { get; set; } = 15;
    public string[] ClosedDays { get; set; } = Array.Empty<string>();
    public string[] ClosedDates { get; set; } = Array.Empty<string>();
    public string PnrAlphabet { get; set; } = "GFLOWERKIN";
    public List<TicketCategory> Categories { get; set; } = new();
    public string MemorialNote { get; set; } = "";
}

public class SmsOptions
{
    public string UrlTemplate { get; set; } = "";
    public string MessageTemplate { get; set; } = "Your verification code is {otp}.";

    /// <summary>
    /// When UrlTemplate is empty (no SMS gateway wired up yet), this code always verifies
    /// successfully regardless of the OTP actually generated - lets you test the full flow
    /// without SMS. Set to null/empty to disable. REMOVE OR LEAVE BLANK BEFORE GOING LIVE:
    /// once UrlTemplate is set, this bypass stops working automatically anyway.
    /// </summary>
    public string? DevBypassOtp { get; set; }
}

public class EasebuzzOptions
{
    public string Env { get; set; } = "test";
    public string Key { get; set; } = "";
    public string Salt { get; set; } = "";
    public bool IsProd => Env.Equals("prod", StringComparison.OrdinalIgnoreCase);
    public string InitiateUrl => IsProd ? "https://pay.easebuzz.in/payment/initiateLink" : "https://testpay.easebuzz.in/payment/initiateLink";
    public string PayBaseUrl => IsProd ? "https://pay.easebuzz.in/pay/" : "https://testpay.easebuzz.in/pay/";
    public string RetrieveUrl => IsProd ? "https://dashboard.easebuzz.in/transaction/v1/retrieve" : "https://testdashboard.easebuzz.in/transaction/v1/retrieve";
}