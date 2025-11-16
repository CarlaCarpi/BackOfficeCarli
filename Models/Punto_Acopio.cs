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

        [Required, StringLength(50)]
        [Column("nombre")]
        public string nombre { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Column("calle")]
        public string calle { get; set; } = string.Empty;

        [Required]
        [Column("altura")]
        public int altura { get; set; }

        [StringLength(10)]
        [Column("departamento")]
        public string? departamento { get; set; }

        [Required]
        [Column("id_provincia")]
        public int id_provincia { get; set; }

        [Required]
        [Column("id_localidad")]
        public int id_localidad { get; set; }

        [StringLength(255)]
        [Column("descripcion")]
        public string? descripcion { get; set; }

        [Column("activo")]
        public bool activo { get; set; } = true;

    }
}
