using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TanaririTickets.Models;

namespace TanaririTickets.Data;

/// <summary>All database access. Every query is parameterised.</summary>
public class BookingRepo
{
    private readonly string _cs;
    private readonly BookingOptions _o;

    public BookingRepo(IConfiguration cfg, IOptions<BookingOptions> o)
    {
        _cs = cfg.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");
        _o = o.Value;
    }

    private SqlConnection Conn() => new(_cs);

    private class SlotCount { public string SlotTime { get; set; } = ""; public int Guests { get; set; } }

    // Slot demand: paid bookings + recent unpaid ones still holding seats.
    private const string BookedCte = @"
        (PaymentStatus = 'success'
         OR (PaymentStatus = 'pending' AND TotalGuests > 0 AND DATEDIFF(MINUTE, UpdatedAt, SYSUTCDATETIME()) < @hold))";

    public async Task<int> CreateAsync(string mobile, string otp, DateTime expiresUtc)
    {
        using var c = Conn();
        return await c.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.Bookings (Mobile, Otp, OtpExpiresAt) OUTPUT INSERTED.BookingId VALUES (@mobile, @otp, @expiresUtc)",
            new { mobile, otp, expiresUtc });
    }

    public async Task<Booking?> GetAsync(int id)
    {
        using var c = Conn();
        return await c.QuerySingleOrDefaultAsync<Booking>("SELECT * FROM dbo.Bookings WHERE BookingId = @id", new { id });
    }

    public async Task SetOtpAsync(int id, string otp, DateTime expiresUtc)
    {
        using var c = Conn();
        await c.ExecuteAsync("UPDATE dbo.Bookings SET Otp=@otp, OtpExpiresAt=@expiresUtc, OtpAttempts=0, UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id",
            new { id, otp, expiresUtc });
    }

    public async Task<int> AddOtpAttemptAsync(int id)
    {
        using var c = Conn();
        return await c.ExecuteScalarAsync<int>(
            "UPDATE dbo.Bookings SET OtpAttempts = OtpAttempts + 1 OUTPUT INSERTED.OtpAttempts WHERE BookingId=@id", new { id });
    }

    public async Task MarkOtpVerifiedAsync(int id)
    {
        using var c = Conn();
        await c.ExecuteAsync("UPDATE dbo.Bookings SET OtpVerified=1, Otp=NULL, UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id", new { id });
    }

    public async Task UpdateDetailsAsync(int id, string name, string email, string city, string? state, string country)
    {
        using var c = Conn();
        await c.ExecuteAsync(@"UPDATE dbo.Bookings SET VisitorName=@name, Email=@email, City=@city, State=@state, Country=@country,
                               UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id",
            new { id, name = name.ToUpperInvariant(), email = email.ToLowerInvariant(), city = city.ToUpperInvariant(),
                  state = state?.ToUpperInvariant(), country = country.ToUpperInvariant() });
    }

    public async Task SetSlotAsync(int id, DateTime date, string slot)
    {
        using var c = Conn();
        await c.ExecuteAsync("UPDATE dbo.Bookings SET VisitDate=@date, SlotTime=@slot, UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id",
            new { id, date = date.Date, slot });
    }

    public async Task<Dictionary<string, int>> BookedBySlotAsync(DateTime date, int excludeId = 0)
    {
        using var c = Conn();
        var rows = await c.QueryAsync<SlotCount>(
            $@"SELECT SlotTime, SUM(TotalGuests) AS Guests FROM dbo.Bookings
               WHERE VisitDate=@date AND SlotTime IS NOT NULL AND BookingId<>@excludeId AND {BookedCte}
               GROUP BY SlotTime",
            new { date = date.Date, excludeId, hold = _o.PendingHoldMinutes });
        return rows.ToDictionary(r => r.SlotTime.Trim(), r => r.Guests);
    }

    /// <summary>Atomically re-checks slot capacity (app-lock per date+slot) and saves the ticket lines.</summary>
    public async Task<bool> ReserveAsync(int id, DateTime date, string slot, List<TicketLine> lines, int guests, decimal total)
    {
        using var c = Conn();
        await c.OpenAsync();
        using var tx = c.BeginTransaction();

        await c.ExecuteAsync("EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
            new { r = $"tanariri-slot:{date:yyyyMMdd}:{slot}" }, tx);

        var booked = await c.ExecuteScalarAsync<int>(
            $@"SELECT ISNULL(SUM(TotalGuests),0) FROM dbo.Bookings
               WHERE VisitDate=@date AND SlotTime=@slot AND BookingId<>@id AND {BookedCte}",
            new { date = date.Date, slot, id, hold = _o.PendingHoldMinutes }, tx);

        if (booked + guests > _o.SlotCapacity) { tx.Rollback(); return false; }

        await c.ExecuteAsync("DELETE FROM dbo.BookingTickets WHERE BookingId=@id", new { id }, tx);
        foreach (var l in lines)
            await c.ExecuteAsync(@"INSERT INTO dbo.BookingTickets (BookingId, CategoryCode, CategoryName, Quantity, Rate, Amount)
                                   VALUES (@id, @CategoryCode, @CategoryName, @Quantity, @Rate, @Amount)",
                new { id, l.CategoryCode, l.CategoryName, l.Quantity, l.Rate, l.Amount }, tx);

        await c.ExecuteAsync(@"UPDATE dbo.Bookings SET TotalGuests=@guests, TotalAmount=@total, PaymentStatus='pending',
                               UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id", new { id, guests, total }, tx);
        tx.Commit();
        return true;
    }

    public async Task<List<TicketLine>> GetLinesAsync(int id)
    {
        using var c = Conn();
        return (await c.QueryAsync<TicketLine>(
            "SELECT CategoryCode, CategoryName, Quantity, Rate, Amount FROM dbo.BookingTickets WHERE BookingId=@id ORDER BY Id",
            new { id })).ToList();
    }

    public async Task SavePaymentAsync(int id, PaymentResult p)
    {
        using var c = Conn();
        await c.ExecuteAsync(@"UPDATE dbo.Bookings SET PaymentStatus=@Status, PaymentMode=@Mode, EasePayId=@EasePayId,
                               BankReference=@BankRef, PaidAmount=@Amount, UpdatedAt=SYSUTCDATETIME() WHERE BookingId=@id",
            new { id, p.Status, p.Mode, p.EasePayId, p.BankRef, p.Amount });
    }

    public async Task SetPnrAsync(int id, string pnr)
    {
        using var c = Conn();
        await c.ExecuteAsync("UPDATE dbo.Bookings SET Pnr=@pnr WHERE BookingId=@id", new { id, pnr });
    }

    public async Task<Booking?> FindPaidAsync(string mobile, string email)
    {
        using var c = Conn();
        return await c.QueryFirstOrDefaultAsync<Booking>(
            @"SELECT TOP 1 * FROM dbo.Bookings WHERE Mobile=@mobile AND LOWER(Email)=@email AND PaymentStatus='success'
              ORDER BY BookingId DESC", new { mobile, email = email.ToLowerInvariant() });
    }
}
