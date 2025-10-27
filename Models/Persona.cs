using System;
using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Persona
    {
        [Key]
        public int id_persona { get; set; }

        // === DATOS PERSONALES ===
        [Required, StringLength(50)]
        public string nombre { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string apellido { get; set; } = string.Empty;

        [Required, Range(1000000, 99999999)]
        public int dni { get; set; }

        // En la BD puede ser NULL; si tu API a veces lo omite, dejalo nullable:
        [DataType(DataType.Date)]
        public DateTime? fechaNacimiento { get; set; }

        [Required, EmailAddress, StringLength(150)]
        public string email { get; set; } = string.Empty; // NOT NULL en SQL

        // === TELÉFONOS ===
        [Required, StringLength(30)]
        public string telefono1 { get; set; } = string.Empty; // NOT NULL en SQL

        [StringLength(30)]
        public string? telefono2 { get; set; }

        // === DIRECCIÓN ===
        [StringLength(100)]
        public string? calle { get; set; }

        public int? altura { get; set; }

        [StringLength(10)]
        public string? departamento { get; set; }

        public int? id_provincia { get; set; }
        public int? id_localidad { get; set; }

        // === OTROS DATOS ===
        [StringLength(200)]
        public string? redesSociales { get; set; }

        [Required] // si permitís null en BD, podés quitarlo
        public int? id_estadoPersona { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime fechaIngreso { get; set; } = DateTime.Now; // DEFAULT GETDATE()

        [DataType(DataType.DateTime)]
        public DateTime? fechaEgreso { get; set; }

        [StringLength(255)]
        public string? motivoEgreso { get; set; }
    }
}
