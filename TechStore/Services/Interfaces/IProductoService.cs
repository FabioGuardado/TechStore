using TechStore.Models;

namespace TechStore.Services.Interfaces
{
    public interface IProductoService
    {
        /// <summary>Obtiene todos los productos, incluyendo su categoría.</summary>
        Task<IEnumerable<Producto>> ObtenerTodosAsync();

        /// <summary>Obtiene un producto por su ID, incluyendo su categoría. Devuelve null si no existe.</summary>
        Task<Producto?> ObtenerPorIdAsync(int id);

        /// <summary>Agrega un producto. Devuelve false si la base de datos no pudo guardarlo.</summary>
        Task<bool> AgregarAsync(Producto producto);

        /// <summary>Actualiza un producto existente. Devuelve false si no existe o no se pudo guardar.</summary>
        Task<bool> EditarAsync(Producto producto);

        /// <summary>Elimina un producto por su ID. Devuelve false si no existe o no se pudo eliminar.</summary>
        Task<bool> EliminarAsync(int id);
    }
}
