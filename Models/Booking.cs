namespace TanaririTickets.Models;

public class Booking
{
    public int BookingId { get; set; }
    public string Mobile { get; set; } = "";
    public string? Otp { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
    public bool OtpVerified { get; set; }
    public string? VisitorName { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public DateTime? VisitDate { get; set; }
    public string? SlotTime { get; set; }
    public int TotalGuests { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = "pending";
    public string? PaymentMode { get; set; }
    public string? EasePayId { get; set; }
    public string? BankReference { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? Pnr { get; set; }
}

public class TicketLine
{
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public record SlotInfo(string Time, int Left, bool Disabled);
public record PaymentResult(string Status, string Mode, string EasePayId, string BankRef, decimal Amount);
