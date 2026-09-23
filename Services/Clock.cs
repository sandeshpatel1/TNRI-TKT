namespace TanaririTickets.Services;

/// <summary>Museum-local time (independent of the server time zone).</summary>
public static class Clock
{
    private static TimeZoneInfo _tz = TimeZoneInfo.Local;

    public static void Init(string id)
    {
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { try { _tz = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); } catch { _tz = TimeZoneInfo.Local; } }
    }

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
    public static DateTime Today => Now.Date;
}
