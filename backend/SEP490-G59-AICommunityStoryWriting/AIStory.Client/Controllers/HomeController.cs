using AIStory.Client.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AIStory.Client.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Story(Guid? id)
        {
            if (!id.HasValue)
                return RedirectToAction(nameof(Index));
            ViewBag.StoryId = id.Value;
            return View();
        }

        public IActionResult Library()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}