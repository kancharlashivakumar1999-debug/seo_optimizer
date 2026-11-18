using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SeoInsight.Web.ViewModels;

namespace SeoInsight.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Dashboard");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string? correlationId)
    {
        var model = new ErrorViewModel
        {
            RequestId = correlationId ?? Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };
        return View(model);
    }
}
