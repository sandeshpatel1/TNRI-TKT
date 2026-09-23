namespace TanaririTickets.Services;

public static class PnrService
{
    /// <summary>Booking number -> letters (same idea as the current site: each digit maps to a letter).</summary>
    public static string Make(int bookingId, string alphabet)
    {
        var chars = bookingId.ToString().Select(d => alphabet[d - '0']);
        return new string(chars.ToArray());
    }
}
