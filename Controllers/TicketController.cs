using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TanaririTickets.Data;
using TanaririTickets.Models;
using TanaririTickets.Services;

namespace TanaririTickets.Controllers;

public class TicketController : Controller
{
    private readonly BookingRepo _repo;
    private readonly SmsService _sms;
    private readonly BookingOptions _o;
    private readonly SmsOptions _smsOpt;

    public TicketController(BookingRepo repo, SmsService sms, IOptions<BookingOptions> o, IOptions<SmsOptions> smsOpt)
    { _repo = repo; _sms = sms; _o = o.Value; _smsOpt = smsOpt.Value; }

    // ---------- Retrieve an existing ticket (mobile + email + OTP) ----------
    [HttpGet("ticket/find")]
    public IActionResult Find() => View(new FindVm());

    [HttpPost("ticket/find")]
    public async Task<IActionResult> Find(FindVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var b = await _repo.FindPaidAsync(vm.Mobile, vm.Email.Trim());
        if (b == null)
        {
            ModelState.AddModelError("", "No paid booking was found for this mobile number and email.");
            return View(vm);
        }

        var otp = OtpHelper.NewOtp();
        var s = HttpContext.Session;
        s.SetInt32("fid", b.BookingId);
        s.SetString("fotp", otp);
        s.SetString("fexp", DateTime.UtcNow.AddMinutes(_o.OtpMinutes).Ticks.ToString());
        s.SetInt32("ftry", 0);
        await _sms.SendOtpAsync(b.Mobile, otp);

        TempData["Mask"] = OtpHelper.Mask(b.Mobile);
        return RedirectToAction(nameof(FindVerify));
    }

    [HttpGet("ticket/verify")]
    public IActionResult FindVerify()
    {
        if (HttpContext.Session.GetInt32("fid") == null) return RedirectToAction(nameof(Find));
        return View(new OtpVm { MaskedMobile = TempData.Peek("Mask") as string ?? "" });
    }

    [HttpPost("ticket/verify")]
    public IActionResult FindVerify(OtpVm vm)
    {
        var s = HttpContext.Session;
        if (s.GetInt32("fid") is not int id) return RedirectToAction(nameof(Find));
        vm.MaskedMobile = TempData.Peek("Mask") as string ?? "";
        if (!ModelState.IsValid) return View(vm);

        var tries = s.GetInt32("ftry") ?? 0;
        long.TryParse(s.GetString("fexp"), out var exp);
        var otp = s.GetString("fotp");

        if (tries >= _o.MaxOtpAttempts || otp == null || exp < DateTime.UtcNow.Ticks)
        { ModelState.AddModelError("", "This code has expired or was tried too many times. Please start again."); return View(vm); }

        if (!OtpHelper.IsDevBypass(vm.Otp, _smsOpt) && !OtpHelper.FixedTimeEquals(otp, vm.Otp))
        {
            s.SetInt32("ftry", tries + 1);
            ModelState.AddModelError("", "Invalid code.");
            ModelState.Remove(nameof(vm.Otp)); vm.Otp = "";
            return View(vm);
        }

        s.Remove("fotp");
        s.SetInt32("view", id);
        return RedirectToAction(nameof(Show), new { id });
    }

    // ---------- The ticket itself ----------
    [HttpGet("ticket/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        if (HttpContext.Session.GetInt32("view") != id) return RedirectToAction(nameof(Find));

        var b = await _repo.GetAsync(id);
        if (b == null || b.PaymentStatus != "success") return RedirectToAction(nameof(Find));

        var pnr = b.Pnr ?? PnrService.Make(id, _o.PnrAlphabet);
        b.Pnr = pnr;
        return View(new TicketVm { Booking = b, Lines = await _repo.GetLinesAsync(id), QrDataUri = QrService.ToDataUri(pnr) });
    }
}