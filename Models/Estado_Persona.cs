using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Estado_Persona
    {

        [Key]
        public int id_estadoPersona { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        public string descripcion { get; set; } = string.Empty;
    }
}
