using Microsoft.EntityFrameworkCore;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services.Interfaces;

namespace TechStore.Services
{
    public class ProductoService : IProductoService
    {
        private readonly TechStoreContext _context;
        private readonly ILogger<ProductoService> _logger;

        public ProductoService(TechStoreContext context, ILogger<ProductoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Producto>> ObtenerTodosAsync()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        public async Task<Producto?> ObtenerPorIdAsync(int id)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ID == id);
        }

        public async Task<bool> AgregarAsync(Producto producto)
        {
            _context.Productos.Add(producto);
            return await GuardarCambiosAsync("agregar", producto.Nombre);
        }

        public async Task<bool> EditarAsync(Producto producto)
        {
            var existente = await _context.Productos.FindAsync(producto.ID);
            if (existente == null) return false;

            existente.Nombre = producto.Nombre;
            existente.Descripcion = producto.Descripcion;
            existente.ImagenUrl = producto.ImagenUrl;
            existente.Precio = producto.Precio;
            existente.Stock = producto.Stock;
            existente.Estado = producto.Estado;
            existente.CategoriaId = producto.CategoriaId;

            return await GuardarCambiosAsync("editar", producto.Nombre);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return false;

            _context.Productos.Remove(producto);
            return await GuardarCambiosAsync("eliminar", producto.Nombre);
        }

        // Traduce los fallos de la BD a false para que el controlador muestre un error y no un 500.
        private async Task<bool> GuardarCambiosAsync(string operacion, string producto)
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Conflicto de concurrencia al {Operacion} el producto {Producto}.", operacion, producto);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de base de datos al {Operacion} el producto {Producto}.", operacion, producto);
                return false;
            }
        }
    }
}
