using Microsoft.AspNetCore.Mvc;

namespace TanaririTickets.Controllers;

public class HomeController : Controller
{
    [Route("Home/Error")]
    public IActionResult Error() => View();
}
