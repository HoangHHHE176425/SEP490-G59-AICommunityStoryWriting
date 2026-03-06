using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class ModeratorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReviewStory(Guid id)
        {
            ViewBag.StoryId = id;
            return View();
        }

        public IActionResult ReviewChapter(Guid id)
        {
            ViewBag.ChapterId = id;
            return View();
        }
    }
}
