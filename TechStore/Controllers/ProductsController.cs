using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Controllers
{
    // Solo usuarios autenticados con alguno de los tres roles pueden consultar productos.
    // Administrador: acceso completo. Vendedor: consultar, agregar y editar.
    // Cliente: únicamente consultar.
    [Authorize(Roles = "Administrador,Vendedor,Cliente")]
    public class ProductsController : Controller
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductoService productoService,
            ICategoriaService categoriaService,
            IWebHostEnvironment env,
            ILogger<ProductsController> logger)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _env = env;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Consultando el listado de productos por el usuario {Usuario}.",
                User.Identity?.Name ?? "anónimo");

            var products = await _productoService.ObtenerTodosAsync();

            _logger.LogInformation("Se consultaron {Cantidad} productos.", products.Count());

            return View(products);
        }

        // cards
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Se solicitó el detalle de un producto sin proporcionar un ID.");
                return NotFound();
            }

            var producto = await _productoService.ObtenerPorIdAsync(id.Value);

            if (producto == null)
            {
                _logger.LogWarning("No se encontró el producto con ID {ProductoId}.", id.Value);
                return NotFound();
            }

            _logger.LogInformation(
                "El usuario {Usuario} consultó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                producto.ID);

            var model = new CardViewModel
            {
                Id = producto.ID,
                Title = producto.Nombre,
                Description = producto.Descripcion,
                ImageUrl = producto.ImagenUrl ?? string.Empty,
                Price = (float)producto.Precio,
                Category = producto.Categoria?.Nombre ?? string.Empty,
                State = producto.Estado,
                Stock = producto.Stock
            };

            return View(model);
        }

        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Agregar()
        {
            ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Agregar(Producto producto, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
                return View(producto);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                _logger.LogInformation(
                    "El usuario {Usuario} está subiendo una imagen para un producto nuevo.",
                    User.Identity?.Name ?? "anónimo");

                var uploads = Path.Combine(_env.WebRootPath, "images");
                Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var filePath = Path.Combine(uploads, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                producto.ImagenUrl = fileName;
            }

            await _productoService.AgregarAsync(producto);

            _logger.LogInformation(
                "El usuario {Usuario} agregó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                producto.ID);

            TempData["SuccessMessage"] = "Producto agregado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Editar(int id)
        {
            var producto = await _productoService.ObtenerPorIdAsync(id);
            if (producto == null) return NotFound();
            ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Vendedor")]
        public async Task<IActionResult> Editar(int id, Producto producto, IFormFile? imageFile)
        {
            if (id != producto.ID) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = (await _categoriaService.ObtenerTodasAsync()).OrderBy(c => c.Nombre).ToList();
                return View(producto);
            }

            var existingProduct = await _productoService.ObtenerPorIdAsync(id);

            if (existingProduct == null)
            {
                _logger.LogWarning("No se encontró el producto {ProductoId} para editarlo.", id);
                return NotFound();
            }

            var oldImage = existingProduct.ImagenUrl;

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images");
                Directory.CreateDirectory(uploads);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var filePath = Path.Combine(uploads, fileName);
                await using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                existingProduct.ImagenUrl = fileName;

                if (!string.IsNullOrEmpty(oldImage))
                {
                    var oldimagePath = Path.Combine(_env.WebRootPath, "images", oldImage);
                    if (System.IO.File.Exists(oldimagePath)) System.IO.File.Delete(oldimagePath);
                }
            }

            // Asignar los valores al objeto que se persistirá
            producto.ImagenUrl = existingProduct.ImagenUrl; // si no se subió nueva imagen conserva

            var success = await _productoService.EditarAsync(producto);
            if (!success) return NotFound();
            _logger.LogInformation(
                "El usuario {Usuario} editó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                id);
            TempData["SuccessMessage"] = "Producto editado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Eliminar productos es exclusivo del Administrador.
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var producto = await _productoService.ObtenerPorIdAsync(id);
            if (producto == null)
            {
                _logger.LogWarning("No se encontró el producto {ProductoId} para eliminarlo.", id);
                return NotFound();
            };

            if (!string.IsNullOrEmpty(producto.ImagenUrl))
            {
                var imagePath = Path.Combine(_env.WebRootPath, "images", producto.ImagenUrl);
                if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
            }

            var success = await _productoService.EliminarAsync(id);
            if (!success) return NotFound();
            _logger.LogInformation(
                "El usuario {Usuario} eliminó el producto {ProductoId}.",
                User.Identity?.Name ?? "anónimo",
                id);
            TempData["SuccessMessage"] = "Producto eliminado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}