using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class CategoriesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}