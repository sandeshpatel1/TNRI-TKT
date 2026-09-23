using Microsoft.Extensions.Options;
using TanaririTickets.Data;
using TanaririTickets.Models;

namespace TanaririTickets.Services;

public class SlotService
{
    private readonly BookingOptions _o;
    private readonly BookingRepo _repo;

    public SlotService(IOptions<BookingOptions> o, BookingRepo repo) { _o = o.Value; _repo = repo; }

    public bool IsClosed(DateTime d) =>
        _o.ClosedDays.Contains(d.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase) ||
        _o.ClosedDates.Contains(d.ToString("yyyy-MM-dd"));

    /// <summary>Returns an error message, or null when the date can be booked.</summary>
    public string? ValidateDate(DateTime d)
    {
        var today = Clock.Today;
        if (d.Date < today) return "This date has already passed.";
        if (d.Date > today.AddDays(_o.AdvanceDays)) return $"Bookings open up to {_o.AdvanceDays} days in advance.";
        if (IsClosed(d)) return "The museum is closed on this day.";
        return null;
    }

    public async Task<List<SlotInfo>> GetSlotsAsync(DateTime date, int excludeBookingId = 0)
    {
        var booked = await _repo.BookedBySlotAsync(date, excludeBookingId);
        var list = new List<SlotInfo>();
        var t = TimeOnly.Parse(_o.SlotStart);
        var end = TimeOnly.Parse(_o.SlotEnd);
        var now = Clock.Now;
        var earliest = now.AddMinutes(_o.LeadMinutes);

        while (t <= end)
        {
            var key = t.ToString("HH:mm");
            var left = Math.Max(0, _o.SlotCapacity - (booked.TryGetValue(key, out var b) ? b : 0));
            var slotDt = date.Date.Add(t.ToTimeSpan());
            list.Add(new SlotInfo(key, left, left == 0 || slotDt < earliest));
            t = t.AddMinutes(_o.SlotIntervalMinutes);
            if (t == TimeOnly.MinValue) break;   // wrapped past midnight
        }
        return list;
    }
}
