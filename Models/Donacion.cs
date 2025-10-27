using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Donacion
    {
        public int id_donacion { get; set; }

        [Required(ErrorMessage = "El tipo de donación es obligatorio.")]
        [RegularExpression("^(M|I)$", ErrorMessage = "Seleccione 'M' (Medicamento) o 'I' (Insumo).")]
        [Display(Name = "Tipo de donación")]
        public string tipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(40, ErrorMessage = "La descripción no puede superar los 40 caracteres.")]
        [Display(Name = "Descripción")]
        public string descripcion { get; set; } = string.Empty;
    }
}
