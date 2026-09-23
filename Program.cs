using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using TanaririTickets.Data;
using TanaririTickets.Models;
using TanaririTickets.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear();   // trust the reverse proxy in front (IIS/nginx)
});
builder.Services.AddHttpClient();

builder.Services.Configure<MuseumOptions>(cfg.GetSection("Museum"));
builder.Services.Configure<BookingOptions>(cfg.GetSection("Booking"));
builder.Services.Configure<SmsOptions>(cfg.GetSection("Sms"));
builder.Services.Configure<EasebuzzOptions>(cfg.GetSection("Easebuzz"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(cfg.GetValue("Booking:SessionMinutes", 10));
    o.Cookie.Name = ".Tanariri.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.Services.AddSingleton<BookingRepo>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<PaymentService>();

Clock.Init(cfg.GetValue("Booking:TimeZone", "Asia/Kolkata")!);

var app = builder.Build();
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllers();
app.Run();
