using Microsoft.AspNetCore.Http;

namespace TechStore.Services.Interfaces
{
    /// <summary>Resultado de guardar una imagen: el nombre del archivo o el mensaje de error para el usuario.</summary>
    public record ResultadoImagen(bool Exito, string? NombreArchivo, string? Error)
    {
        public static ResultadoImagen Guardada(string nombreArchivo) => new(true, nombreArchivo, null);

        public static ResultadoImagen Fallida(string error) => new(false, null, error);
    }

    public interface IImagenService
    {
        /// <summary>Valida y guarda la imagen en wwwroot/images. No lanza por errores de validación ni de E/S.</summary>
        Task<ResultadoImagen> GuardarAsync(IFormFile archivo);

        /// <summary>Elimina una imagen de wwwroot/images. Devuelve false si no existe o no se pudo borrar.</summary>
        bool Eliminar(string? nombreArchivo);
    }
}
