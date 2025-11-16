namespace SantaRamona.Backoffice.Models
{
    public class EventoUsuarioViewModel
    {
        public string Entidad { get; set; } = "";   // Persona / Animal / Pensión
        public int IdRegistro { get; set; }         // id_persona / id_animal / id_pension
        public string NombreRegistro { get; set; } = "";
        public string Accion { get; set; } = "";    // CREAR / MODIFICAR
        public DateTime Fecha { get; set; }
    }
}