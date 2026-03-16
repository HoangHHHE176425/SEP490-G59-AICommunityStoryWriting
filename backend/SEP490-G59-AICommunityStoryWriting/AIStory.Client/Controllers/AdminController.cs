using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    /// <summary>Dashboard Admin: theo dõi hoạt động duyệt của Moderator. Client dùng JWT từ localStorage; API sẽ trả 403 nếu không phải ADMIN.</summary>
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
