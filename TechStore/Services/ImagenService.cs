using TechStore.Services.Interfaces;

namespace TechStore.Services
{
    public class ImagenService : IImagenService
    {
        public const long TamanoMaximoBytes = 2 * 1024 * 1024;

        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImagenService> _logger;

        public ImagenService(IWebHostEnvironment env, ILogger<ImagenService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<ResultadoImagen> GuardarAsync(IFormFile archivo)
        {
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

            if (!ExtensionesPermitidas.Contains(extension))
            {
                _logger.LogWarning(
                    "Se rechazó la imagen por su extensión ({Extension}, {Bytes} bytes).",
                    string.IsNullOrEmpty(extension) ? "(sin extensión)" : extension,
                    archivo.Length);
                return ResultadoImagen.Fallida("La imagen debe ser .jpg, .jpeg, .png, .webp o .gif.");
            }

            if (archivo.Length > TamanoMaximoBytes)
            {
                _logger.LogWarning(
                    "Se rechazó la imagen por su tamaño ({Extension}, {Bytes} bytes).",
                    extension,
                    archivo.Length);
                return ResultadoImagen.Fallida("La imagen no puede superar los 2 MB.");
            }

            var nombre = $"{Guid.NewGuid()}{extension}";
            var ruta = Path.Combine(CarpetaImagenes, nombre);

            try
            {
                Directory.CreateDirectory(CarpetaImagenes);
                await using var stream = new FileStream(ruta, FileMode.Create);
                await archivo.CopyToAsync(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogError(ex, "No se pudo guardar la imagen {Imagen}.", nombre);
                IntentarBorrar(ruta);
                return ResultadoImagen.Fallida("No se pudo guardar la imagen. Intenta de nuevo.");
            }

            _logger.LogInformation("Se guardó la imagen {Imagen} ({Bytes} bytes).", nombre, archivo.Length);
            return ResultadoImagen.Guardada(nombre);
        }

        public bool Eliminar(string? nombreArchivo)
        {
            if (string.IsNullOrEmpty(nombreArchivo)) return false;

            // Las imágenes sembradas guardan "/images/x.jpg": solo cuenta el nombre, nunca una ruta.
            var nombre = Path.GetFileName(nombreArchivo);
            var ruta = Path.Combine(CarpetaImagenes, nombre);

            if (!File.Exists(ruta))
            {
                _logger.LogWarning("La imagen {Imagen} no existe; no se eliminó ningún archivo.", nombre);
                return false;
            }

            try
            {
                File.Delete(ruta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogError(ex, "No se pudo eliminar la imagen {Imagen}.", nombre);
                return false;
            }

            _logger.LogInformation("Se eliminó la imagen {Imagen}.", nombre);
            return true;
        }

        private string CarpetaImagenes => Path.Combine(_env.WebRootPath, "images");

        private void IntentarBorrar(string ruta)
        {
            try
            {
                if (File.Exists(ruta)) File.Delete(ruta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "No se pudo limpiar el archivo parcial {Ruta}.", Path.GetFileName(ruta));
            }
        }
    }
}
