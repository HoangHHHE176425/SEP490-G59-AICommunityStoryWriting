using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class StoriesController : Controller
    {
        private readonly IConfiguration _configuration;

        public StoriesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewBag.DefaultAuthorId = _configuration["DefaultAuthorIdForStories"] ?? "";
            return View();
        }

        public IActionResult Details(Guid? id)
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
