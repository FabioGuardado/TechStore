namespace TechStore.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public int Codigo { get; set; } = 500;

        public string Titulo { get; set; } = "Algo salió mal";

        public string Mensaje { get; set; } = "Ocurrió un error inesperado al procesar tu solicitud. Intenta de nuevo en unos minutos.";
    }
}
