using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class ChaptersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ByStory(Guid? storyId)
        {
            if (!storyId.HasValue)
            {
                return RedirectToAction("Index");
            }
            ViewBag.StoryId = storyId.Value;
            return View();
        }

        public IActionResult Read(Guid? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index", "Stories");
            }
            ViewBag.ChapterId = id.Value;
            return View();
        }
    }
}