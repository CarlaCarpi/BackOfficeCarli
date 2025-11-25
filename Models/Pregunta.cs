using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Pregunta
    {
        [Key]
        public int id_pregunta { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un tipo de formulario.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un tipo de formulario.")]
        public int id_tipoFormulario { get; set; }

        [Required(ErrorMessage = "La pregunta es obligatoria.")]
        [StringLength(1000, ErrorMessage = "La pregunta no puede superar los 1000 caracteres.")]
        [Display(Name = "Texto de la pregunta")]
        public string pregunta { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "El orden debe ser un número mayor o igual a 0.")]
        [Display(Name = "Orden")]
        public int? orden { get; set; }
        public bool activo { get; set; } = true;
    }
}