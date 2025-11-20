using System;
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

        [Required(ErrorMessage = "El sexo es obligatorio.")]
        [RegularExpression("^(M|H)$", ErrorMessage = "El sexo debe ser 'M' (Macho) o 'H' (Hembra).")]
        public string sexo { get; set; } = string.Empty;

        //NUEVO: edadValor (obligatorio)
        //Nota: antes tenías edad en años con [Range(0,40)].
        //Ahora se separa en valor + unidad. Dejamos un rango amplio y el UI puede reforzar por unidad.
        [Range(0, 480, ErrorMessage = "Ingrese un valor de edad válido.")]
        public int edadValor { get; set; }

        //NUEVO: edadUnidad (obligatorio, M/A)
        [Required(ErrorMessage = "La unidad de edad es obligatoria.")]
        [RegularExpression("^(M|A)$", ErrorMessage = "La unidad de edad debe ser 'M' (Meses) o 'A' (Años).")]
        public string edadUnidad { get; set; } = "A";

        //NUEVO: imagen (opcional). Si no la enviás, queda null.
        [Required(ErrorMessage = "La imagen es obligatorio.")]
        public byte[] imagen { get; set; } 

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una especie válida.")]
        [Required(ErrorMessage = "La especie es obligatorio.")]
        public int id_especie { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tamaño válido.")]
        [Required(ErrorMessage = "El tamaño es obligatorio.")]
        public int id_tamano { get; set; }       

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un estado válido.")]
        [Required(ErrorMessage = "El estado es obligatorio.")]
        public int id_estadoAnimal { get; set; }
        //validamos que sea > 0.
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una persona válida.")]
        public int? id_persona { get; set; }

        //validamos que sea > 0.
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una pensión válida.")]
        public int? id_pension { get; set; }

        //(obligatorio > 0)
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un usuario válido.")]
        public int id_usuario { get; set; }

        //lo setea SQL por default
        public DateTime? fechaIngreso { get; set; }

        //opcional
        public DateTime? fechaAdopcion { get; set; }

        //opcional
        public string? historia { get; set; }

        //opcional
        public string? seguimiento { get; set; }

        //lo setea SQL por default
        public DateTime? fechaModificacion { get; set; }

    }
}
