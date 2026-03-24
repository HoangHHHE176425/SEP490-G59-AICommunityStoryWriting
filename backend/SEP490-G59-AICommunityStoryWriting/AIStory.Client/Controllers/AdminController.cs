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

        /// <summary>Đã gộp vào Admin/Index — tab Đơn gửi admin → Compliance.</summary>
        public IActionResult ComplianceLocks()
        {
            return RedirectToAction(nameof(Index), new { esc = "compliance" });
        }
    }
}
