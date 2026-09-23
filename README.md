# Tanariri Musical Museum — Ticket Booking

## Run
1. Install the .NET SDK (this project targets **net8.0** — ".NET 22" does not exist yet;
   change `<TargetFramework>` in `TanaririTickets.csproj` to `net9.0`/`net10.0` if a newer SDK is required).
2. Create the database: run `Database/schema.sql` against your SQL Server instance.
3. Edit `appsettings.json`:
   - `ConnectionStrings:Default`
   - `Museum:*` (address, email, phone, hours, links)
   - `Sms:UrlTemplate` (your SMS gateway; without it, OTPs are written to the console log so you can test locally)
   - `Easebuzz:Key` / `Easebuzz:Salt` (payment gateway; without them, checkout stops with a friendly error)
   - `Booking:Categories` — ticket types and prices
4. `dotnet restore && dotnet run`

## Flow
Mobile → OTP → Visitor details → Plan visit (date + slot) → Select tickets → Easebuzz payment → Ticket (QR).
"Fetch my ticket" lets a returning visitor retrieve a paid booking via mobile + email + OTP.

## Notes
- All prices and slot capacity are enforced server-side from `appsettings.json`, never trusted from the browser.
- Slot capacity is re-checked with a SQL application lock at reservation time to prevent overbooking.
- Payment status is only ever trusted from Easebuzz's own "retrieve" API, never from the redirect alone.
- The logo has been converted to a transparent PNG at `wwwroot/images/` (full + small + favicon).
