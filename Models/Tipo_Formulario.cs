using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Tipo_Formulario
    {
        public int id_tipoFormulario { get; set; }

        [Required(ErrorMessage = "El tipo de formulario es obligatorio.")]
        [StringLength(100, ErrorMessage = "El tipo no puede superar los 50 caracteres.")]
        public string tipo { get; set; } = string.Empty;

        // Estado: solo Activo / Inactivo, viene de botones, no se tipea manualmente
        [Required(ErrorMessage = "El estado es obligatorio.")]
        [RegularExpression("^(Activo|Inactivo)$", ErrorMessage = "El estado debe ser Activo o Inactivo.")]
        [StringLength(50)]
        public string Estado { get; set; } = "Activo";
    }
}
