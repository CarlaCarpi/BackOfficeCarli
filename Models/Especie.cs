using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Especie
    {
        public int id_especie { get; set; }

        [Required(ErrorMessage = "La especie es obligatoria")]
        public string especie { get; set; }
    }
}
