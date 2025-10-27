using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Pregunta
    {
        [Key]
        public int id_pregunta { get; set; }

        [Required(ErrorMessage = "La pregunta es obligatoria.")]
        [StringLength(300, ErrorMessage = "La pregunta no puede superar los 300 caracteres.")]
        [Display(Name = "Texto de la pregunta")]
        public string pregunta { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un tipo de formulario.")]
        [Display(Name = "Tipo de formulario")]
        public int id_tipoFormulario { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El orden debe ser un número mayor o igual a 0.")]
        [Display(Name = "Orden")]
        public int? orden { get; set; }
    }
}
