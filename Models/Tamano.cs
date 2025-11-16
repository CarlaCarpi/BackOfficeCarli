using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Tamano
    {
        public int id_tamano { get; set; }

        [Required(ErrorMessage = "El tamaño es obligatoria")]
        public string tamano { get; set; }
    }
}