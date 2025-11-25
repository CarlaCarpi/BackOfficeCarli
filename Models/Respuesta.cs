using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace SantaRamona.Backoffice.Models
{
    public class Respuesta
    {
        [Key]
        public int id_respuesta { get; set; }

        [StringLength(500, ErrorMessage = "La respuesta no puede superar los 500 caracteres.")]
        [RegularExpression(
    @"^[A-Za-zÁÉÍÓÚáéíóúÑñ0-9\s]+$",
    ErrorMessage = "No se permiten caracteres especiales."
)]
        //[Required(ErrorMessage = "La respuesta es obligatoria.")]
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
