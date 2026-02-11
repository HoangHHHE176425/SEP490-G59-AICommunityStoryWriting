using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    // Không dùng [Authorize] ở đây vì Client dùng JWT từ localStorage
    // Frontend JavaScript sẽ kiểm tra authentication và role
    public class CategoriesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}