using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SantaRamona.Backoffice.Models
{
    public class Formulario
    {
        public int id_formulario { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una persona válida.")]
        public int id_persona { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tipo de formulario válido.")]
        public int id_tipoFormulario { get; set; }

        // Lo setea SQL por defecto (GETDATE); lo dejo nullable para no obligarlo desde el formulario.
        public DateTime? fechaEnvio { get; set; }

        // No se tipea en la UI: lo seteás con botones o un select.
        [Required]
        [StringLength(50)]
        [RegularExpression("^(Pendiente|Aprobado|Denegado)$",
            ErrorMessage = "Estado inválido.")]
        public string estado { get; set; } = "Pendiente";
    }
}