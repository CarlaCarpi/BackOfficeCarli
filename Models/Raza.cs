using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Raza
    {
        public int id_raza { get; set; }

        [Required(ErrorMessage = "La raza es obligatoria")]
        public string raza { get; set; }
    }
}