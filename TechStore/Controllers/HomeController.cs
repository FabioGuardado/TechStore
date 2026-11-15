using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductoService _productoService;

        private readonly ILogger<HomeController> _logger;

        public HomeController(IProductoService productoService, ILogger<HomeController> logger)
        {
            _productoService = productoService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {

            var productos = await _productoService.ObtenerTodosAsync();

            var products = productos
                .GroupBy(p => p.CategoriaId)
                .Select(g => g.OrderBy(p => p.Stock).ThenBy(p => p.ID).First())
                .Select(p => new CardViewModel
                {
                    Id = p.ID,
                    Title = p.Nombre,
                    Description = p.Descripcion,
                    ImageUrl = p.ImagenUrl ?? string.Empty,
                    Price = (float)p.Precio,
                    Category = p.Categoria != null ? p.Categoria.Nombre : string.Empty,
                    State = p.Estado,
                    Stock = p.Stock
                })
                .ToList();

            return View(products);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var excepcion = HttpContext.Features.Get<IExceptionHandlerFeature>();

            _logger.LogError(
                excepcion?.Error,
                "Error no controlado en {Ruta}. RequestId: {RequestId}.",
                excepcion?.Path ?? "(desconocida)",
                requestId);

            return View(new ErrorViewModel { RequestId = requestId });
        }

        // Página para respuestas 400, 403 y 404 sin contenido (ver UseStatusCodePagesWithReExecute).
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCode(int code)
        {
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Codigo = code
            };

            var original = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            _logger.LogWarning(
                "Respuesta {Codigo} en la ruta {Ruta}. RequestId: {RequestId}.",
                code,
                original?.OriginalPath ?? "(desconocida)",
                model.RequestId);

            switch (code)
            {
                case 404:
                    model.Titulo = "No encontramos esa página";
                    model.Mensaje = "La página o el producto que buscas no existe o fue eliminado.";
                    break;
                case 403:
                    model.Titulo = "Acceso denegado";
                    model.Mensaje = "No tienes permiso para ver este contenido.";
                    break;
                case 400:
                    model.Titulo = "Solicitud no válida";
                    model.Mensaje = "No pudimos procesar tu solicitud. Revisa los datos e intenta de nuevo.";
                    break;
            }

            return View("Error", model);
        }
    }
}
