using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Estado_Animal
    {
        public int id_estadoAnimal { get; set; }

        [Required(ErrorMessage = "El estado es obligatoria")]
        public string estado { get; set; }
    }
}