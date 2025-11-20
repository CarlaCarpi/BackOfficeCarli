namespace SantaRamona.Backoffice.Models
{
    public class EventoUsuarioViewModel
    {
        public int IdUsuario { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public string Entidad { get; set; } = "";
        public int IdRegistro { get; set; }
        public string NombreRegistro { get; set; } = "";
        public string Accion { get; set; } = "";
        public DateTime Fecha { get; set; }
    }
}