using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TanaririTickets.Data;
using TanaririTickets.Models;
using TanaririTickets.Services;

namespace TanaririTickets.Controllers;

public class BookingController : Controller
{
    private readonly BookingRepo _repo;
    private readonly SlotService _slots;
    private readonly SmsService _sms;
    private readonly PaymentService _pay;
    private readonly BookingOptions _o;
    private readonly SmsOptions _smsOpt;

    public BookingController(BookingRepo repo, SlotService slots, SmsService sms, PaymentService pay,
        IOptions<BookingOptions> o, IOptions<SmsOptions> smsOpt)
    { _repo = repo; _slots = slots; _sms = sms; _pay = pay; _o = o.Value; _smsOpt = smsOpt.Value; }

    private int? Bid => HttpContext.Session.GetInt32("bid");
    private bool Verified => HttpContext.Session.GetInt32("ok") == 1;

    // ---------- Step 1: mobile ----------
    [HttpGet("/")]
    public IActionResult Start() { ViewData["Step"] = 1; return View(new MobileVm()); }

    [HttpPost("/")]
    public async Task<IActionResult> Start(MobileVm vm)
    {
        ViewData["Step"] = 1;
        if (!ModelState.IsValid) return View(vm);

        var otp = OtpHelper.NewOtp();
        var id = await _repo.CreateAsync(vm.Mobile, otp, DateTime.UtcNow.AddMinutes(_o.OtpMinutes));
        await _sms.SendOtpAsync(vm.Mobile, otp);

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32("bid", id);
        HttpContext.Session.SetString("otpAt", DateTime.UtcNow.Ticks.ToString());
        return RedirectToAction(nameof(Verify));
    }

    // ---------- Step 2: OTP ----------
    [HttpGet("verify")]
    public async Task<IActionResult> Verify()
    {
        if (Bid is not int id) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b == null) return RedirectToAction(nameof(Start));
        ViewData["Step"] = 1;
        return View(new OtpVm { MaskedMobile = OtpHelper.Mask(b.Mobile) });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(OtpVm vm)
    {
        if (Bid is not int id) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b == null) return RedirectToAction(nameof(Start));

        ViewData["Step"] = 1;
        vm.MaskedMobile = OtpHelper.Mask(b.Mobile);
        if (!ModelState.IsValid) return View(vm);

        if (b.OtpAttempts >= _o.MaxOtpAttempts)
        { ModelState.AddModelError("", "Too many wrong attempts. Please request a new code."); return View(vm); }

        var valid = OtpHelper.IsDevBypass(vm.Otp, _smsOpt)
            || (b.Otp != null && b.OtpExpiresAt > DateTime.UtcNow && OtpHelper.FixedTimeEquals(b.Otp, vm.Otp));
        if (!valid)
        {
            await _repo.AddOtpAttemptAsync(id);
            ModelState.AddModelError("", b.OtpExpiresAt <= DateTime.UtcNow ? "This code has expired. Please request a new one." : "Invalid code.");
            ModelState.Remove(nameof(vm.Otp)); vm.Otp = "";
            return View(vm);
        }

        await _repo.MarkOtpVerifiedAsync(id);
        HttpContext.Session.SetInt32("ok", 1);
        return RedirectToAction(nameof(Details));
    }

    [HttpPost("verify/resend")]
    public async Task<IActionResult> Resend()
    {
        if (Bid is not int id) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b == null) return RedirectToAction(nameof(Start));

        long.TryParse(HttpContext.Session.GetString("otpAt"), out var last);
        if (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) < TimeSpan.FromSeconds(30))
        { TempData["Msg"] = "Please wait a few seconds before requesting another code."; return RedirectToAction(nameof(Verify)); }

        var otp = OtpHelper.NewOtp();
        await _repo.SetOtpAsync(id, otp, DateTime.UtcNow.AddMinutes(_o.OtpMinutes));
        await _sms.SendOtpAsync(b.Mobile, otp);
        HttpContext.Session.SetString("otpAt", DateTime.UtcNow.Ticks.ToString());
        TempData["Msg"] = "A new code has been sent.";
        return RedirectToAction(nameof(Verify));
    }

    // ---------- Step 3: visitor details ----------
    [HttpGet("details")]
    public async Task<IActionResult> Details()
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b == null) return RedirectToAction(nameof(Start));
        ViewData["Step"] = 2;
        return View(new DetailsVm
        {
            Name = b.VisitorName ?? "",
            Email = b.Email ?? "",
            City = b.City ?? "",
            State = b.State,
            Country = string.IsNullOrEmpty(b.Country) ? "India" : b.Country
        });
    }

    [HttpPost("details")]
    public async Task<IActionResult> Details(DetailsVm vm)
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));
        ViewData["Step"] = 2;

        if (vm.Country.Trim().Equals("India", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(vm.State))
            ModelState.AddModelError(nameof(vm.State), "State is required for India.");
        if (!ModelState.IsValid) return View(vm);

        await _repo.UpdateDetailsAsync(id, vm.Name.Trim(), vm.Email.Trim(), vm.City.Trim(), vm.State?.Trim(), vm.Country.Trim());
        return RedirectToAction(nameof(Plan));
    }

    // ---------- Step 4: date + slot ----------
    [HttpGet("plan")]
    public async Task<IActionResult> Plan()
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b == null || string.IsNullOrEmpty(b.VisitorName)) return RedirectToAction(nameof(Details));
        ViewData["Step"] = 3;
        return View(new PlanVm
        {
            Today = Clock.Today.ToString("yyyy-MM-dd"),
            MaxDate = Clock.Today.AddDays(_o.AdvanceDays).ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("plan/slots")]
    public async Task<IActionResult> Slots(string date)
    {
        if (Bid is not int id || !Verified) return Unauthorized();
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return BadRequest();

        var err = _slots.ValidateDate(d);
        if (err != null) return Json(new { closed = true, message = err, slots = Array.Empty<SlotInfo>() });
        return Json(new { closed = false, message = "", slots = await _slots.GetSlotsAsync(d, id) });
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Plan(PlanVm vm)
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));

        if (!DateTime.TryParseExact(vm.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ||
            string.IsNullOrEmpty(vm.Slot))
        { TempData["Error"] = "Please choose a date and a time slot."; return RedirectToAction(nameof(Plan)); }

        var err = _slots.ValidateDate(d);
        var slot = (await _slots.GetSlotsAsync(d, id)).FirstOrDefault(s => s.Time == vm.Slot);
        if (err != null || slot == null || slot.Disabled)
        { TempData["Error"] = err ?? "That time slot is no longer available."; return RedirectToAction(nameof(Plan)); }

        await _repo.SetSlotAsync(id, d, slot.Time);
        return RedirectToAction(nameof(Select));
    }

    // ---------- Step 5: tickets ----------
    [HttpGet("tickets")]
    public async Task<IActionResult> Select(string audience = "Indian")
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b?.VisitDate == null || b.SlotTime == null) return RedirectToAction(nameof(Plan));

        audience = audience == "Foreigner" ? "Foreigner" : "Indian";
        var booked = await _repo.BookedBySlotAsync(b.VisitDate.Value, id);
        var left = Math.Max(0, _o.SlotCapacity - (booked.TryGetValue(b.SlotTime.Trim(), out var n) ? n : 0));

        ViewData["Step"] = 4;
        return View(new SelectVm
        {
            Audience = audience,
            VisitDate = b.VisitDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            Slot = b.SlotTime.Trim(),
            SlotLeft = left,
            Categories = _o.Categories.Where(c => c.Audience == audience).ToList(),
            Note = _o.MemorialNote
        });
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> Pay(string audience, Dictionary<string, int> qty)
    {
        if (Bid is not int id || !Verified) return RedirectToAction(nameof(Start));
        var b = await _repo.GetAsync(id);
        if (b?.VisitDate == null || b.SlotTime == null || string.IsNullOrEmpty(b.Email)) return RedirectToAction(nameof(Plan));

        audience = audience == "Foreigner" ? "Foreigner" : "Indian";
        IActionResult Back(string msg) { TempData["Error"] = msg; return RedirectToAction(nameof(Select), new { audience }); }

        // The visit must still be bookable (date could have rolled over, lead time passed, etc.)
        var slotOk = (await _slots.GetSlotsAsync(b.VisitDate.Value, id)).FirstOrDefault(s => s.Time == b.SlotTime.Trim());
        if (_slots.ValidateDate(b.VisitDate.Value) != null || slotOk == null || slotOk.Disabled)
            return Back("Your selected slot is no longer available. Please choose another.");

        // Prices always come from server configuration - never from the browser.
        var lines = new List<TicketLine>();
        foreach (var c in _o.Categories.Where(c => c.Audience == audience))
        {
            var q = qty.TryGetValue(c.Code, out var v) ? v : 0;
            if (q < 0 || q > _o.SlotCapacity) return Back("Invalid ticket quantity.");
            if (q > 0) lines.Add(new TicketLine { CategoryCode = c.Code, CategoryName = c.Name, Quantity = q, Rate = c.Price, Amount = c.Price * q });
        }

        var guests = lines.Sum(l => l.Quantity);
        var total = lines.Sum(l => l.Amount);
        if (guests == 0) return Back("Please select at least one ticket.");
        if (total <= 0) return Back("Please include at least one paid ticket.");
        if (guests > _o.SlotCapacity) return Back($"A maximum of {_o.SlotCapacity} visitors are allowed per slot.");

        if (!await _repo.ReserveAsync(id, b.VisitDate.Value, b.SlotTime.Trim(), lines, guests, total))
            return Back("Sorry, not enough seats are left in this slot. Please reduce tickets or choose another slot.");

        b = await _repo.GetAsync(id);
        var resultUrl = Url.Action("Result", "Payment", new { id }, Request.Scheme)!;
        var (url, error) = await _pay.InitiateAsync(b!, resultUrl, resultUrl);
        if (url == null) return Back(error ?? "Payment could not be started.");
        return Redirect(url);
    }
}