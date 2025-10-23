using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SantaRamona.Backoffice.Models
{
    public class Animal
    {
        public int id_animal { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string nombre { get; set; } = string.Empty;

        [Range(0, 40, ErrorMessage = "Ingrese una edad válida (en años).")]
        public int edad { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una especie válida.")]
        public int id_especie { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tamaño válido.")]
        public int id_tamano { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una raza válida.")]
        public int id_raza { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un estado válido.")]
        [JsonPropertyName("id_estado")]        // la API usa 'id_estado'
        public int id_estadoAnimal { get; set; }

        public int? id_persona { get; set; }    // opcional

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un usuario válido.")]
        public int id_usuario { get; set; }     // la API lo requiere > 0

        public string? historia { get; set; }   // opcional

        public int? id_pension { get; set; }    // opcional
    }
}