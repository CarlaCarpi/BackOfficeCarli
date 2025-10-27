using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Respuesta
    {
        [Key]
        public int id_respuesta { get; set; }

        [Required(ErrorMessage = "La respuesta es obligatoria.")]
        [Display(Name = "Texto de la respuesta")]
        public string respuesta { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un formulario.")]
        [Display(Name = "Formulario")]
        public int id_formulario { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una pregunta.")]
        [Display(Name = "Pregunta")]
        public int id_pregunta { get; set; }
    }
}
