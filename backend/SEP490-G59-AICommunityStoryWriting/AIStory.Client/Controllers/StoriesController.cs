using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class StoriesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index");
            }
            ViewBag.StoryId = id.Value;
            return View();
        }
    }
}
