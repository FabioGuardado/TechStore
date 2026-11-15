using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TechStore.Controllers
{
    public class ContactController : Controller
    {
        private readonly ILogger<ContactController> _logger;

        public ContactController(ILogger<ContactController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation(
                "El usuario {Usuario} accedió a la página de contacto.",
                User.Identity?.Name ?? "anónimo");

            return View();
        }
    }
}
