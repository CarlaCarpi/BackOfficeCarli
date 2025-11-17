using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace SantaRamona.Backoffice.Models
{
    public class Punto_Acopio
    {
        [Key]

        [Column("id_puntoAcopio")]
        public int id_puntoAcopio { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜáéíóúüÑñ\s]+$",ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        [Column("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La calle es obligatorio.")]
        [StringLength(100, ErrorMessage = "La calle no puede superar 100 caracteres.")]
        [Column("calle")]
        public string calle { get; set; } = string.Empty;

        [Required(ErrorMessage = "La altura es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "La altura debe ser mayor a 0.")]
        [Column("altura")]
        public int altura { get; set; }

        [StringLength(10)]
        [Column("departamento")]
        public string? departamento { get; set; }

        [Required(ErrorMessage = "Seleccionar una provincia es obligatorio.")]
        [Column("id_provincia")]
        public int id_provincia { get; set; }

        [Required(ErrorMessage = "Seleccionar una localidad es obligatorio.")]
        [Column("id_localidad")]
        public int id_localidad { get; set; }

        [StringLength(255)]
        [Column("descripcion")]
        public string? descripcion { get; set; }

        [Column("activo")]
        public bool activo { get; set; } = true;

    }
}
