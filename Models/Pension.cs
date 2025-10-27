using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaRamona.Backoffice.Models
{
    public class Pension
    {
        [Key]
        public int id_pension { get; set; }

        // === DATOS PERSONALES ===
        [Required, StringLength(50)]
        public string? nombre { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string? email { get; set; } = string.Empty; // NOT NULL en SQL

        // === TELÉFONOS ===
        [Required, StringLength(30)]
        public string telefono1 { get; set; } = string.Empty; // NOT NULL en SQL

        [StringLength(30)]
        public string? telefono2 { get; set; }

        // === DIRECCIÓN ===
        [StringLength(100)]
        public string calle { get; set; }

        public int altura { get; set; }

        [StringLength(10)]
        public string? departamento { get; set; }

        public int id_provincia { get; set; }
        public int id_localidad { get; set; }

        // === OTROS DATOS ===
        [StringLength(200)]
        public string? redesSociales { get; set; }

        [Required] // si permitís null en BD, podés quitarlo
        public int id_estadoPension { get; set; }

        [Required] // si permitís null en BD, podés quitarlo
        public int id_usuario { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime fechaIngreso { get; set; } = DateTime.Now; // DEFAULT GETDATE()

        [DataType(DataType.DateTime)]
        public DateTime? fechaEgreso { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? montoDia { get; set; }
    }
}
