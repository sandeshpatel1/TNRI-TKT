using System.ComponentModel.DataAnnotations;

namespace TanaririTickets.Models;

public class MobileVm
{
    [Required(ErrorMessage = "Please enter your mobile number.")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    public string Mobile { get; set; } = "";
}

public class OtpVm
{
    public string MaskedMobile { get; set; } = "";
    [Required(ErrorMessage = "Enter the 4-digit code.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Enter the 4-digit code.")]
    public string Otp { get; set; } = "";
}

public class DetailsVm
{
    [Required(ErrorMessage = "Full name is required."), StringLength(150)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email."), StringLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "City is required."), StringLength(100)]
    public string City { get; set; } = "";

    [StringLength(100)]
    public string? State { get; set; }

    [Required(ErrorMessage = "Country is required."), StringLength(60)]
    public string Country { get; set; } = "India";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the terms and conditions.")]
    public bool Accept { get; set; }
}

public class PlanVm
{
    public string Today { get; set; } = "";
    public string MaxDate { get; set; } = "";
    public string? Date { get; set; }
    public string? Slot { get; set; }
}

public class SelectVm
{
    public string Audience { get; set; } = "Indian";
    public string VisitDate { get; set; } = "";
    public string Slot { get; set; } = "";
    public int SlotLeft { get; set; }
    public List<TicketCategory> Categories { get; set; } = new();
    public string Note { get; set; } = "";
}

public class FindVm
{
    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number.")]
    public string Mobile { get; set; } = "";

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email.")]
    public string Email { get; set; } = "";
}

public class TicketVm
{
    public Booking Booking { get; set; } = new();
    public List<TicketLine> Lines { get; set; } = new();
    public string QrDataUri { get; set; } = "";
}
