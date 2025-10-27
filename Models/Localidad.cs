namespace SantaRamona.Backoffice.Models
{
    public class Localidad
    {
        public int id_localidad { get; set; }
        public int id_provincia { get; set; }
        public string nombre { get; set; } = string.Empty;
        public int? codigopostal { get; set; }   // hacerlo nullable por si viene vacío
    }
}