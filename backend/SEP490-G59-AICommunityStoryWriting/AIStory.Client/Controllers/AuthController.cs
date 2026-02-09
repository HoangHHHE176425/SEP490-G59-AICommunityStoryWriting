using Microsoft.AspNetCore.Mvc;

namespace AIStory.Client.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }

        public IActionResult VerifyOtp()
        {
            return View();
        }
    }
}
