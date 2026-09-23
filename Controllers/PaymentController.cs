using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TanaririTickets.Data;
using TanaririTickets.Models;
using TanaririTickets.Services;

namespace TanaririTickets.Controllers;

public class PaymentController : Controller
{
    private readonly BookingRepo _repo;
    private readonly PaymentService _pay;
    private readonly BookingOptions _o;

    public PaymentController(BookingRepo repo, PaymentService pay, IOptions<BookingOptions> o)
    { _repo = repo; _pay = pay; _o = o.Value; }

    /// <summary>Easebuzz sends the customer back here (POST) for both success and failure.
    /// The outcome is never trusted from the request - we ask Easebuzz directly.</summary>
    [AcceptVerbs("GET", "POST")]
    [IgnoreAntiforgeryToken]
    [Route("payment/result/{id:int}")]
    public async Task<IActionResult> Result(int id)
    {
        var b = await _repo.GetAsync(id);
        if (b == null) return NotFound();

        if (b.PaymentStatus != "success" && b.TotalAmount > 0)
        {
            var r = await _pay.RetrieveAsync(b);
            if (r != null && !string.IsNullOrEmpty(r.Status))
            {
                var status = r.Status;
                if (status == "success" && r.Amount != b.TotalAmount) status = "amount_mismatch";
                await _repo.SavePaymentAsync(id, r with { Status = status });
                b.PaymentStatus = status;
            }
        }

        if (b.PaymentStatus == "success")
        {
            if (string.IsNullOrEmpty(b.Pnr))
                await _repo.SetPnrAsync(id, PnrService.Make(id, _o.PnrAlphabet));

            HttpContext.Session.SetInt32("view", id);   // lets this browser open the ticket page
            return RedirectToAction("Show", "Ticket", new { id });
        }
        return RedirectToAction(nameof(Failed));
    }

    [HttpGet("payment/failed")]
    public IActionResult Failed() => View();
}
