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
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string? nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un email válido.")]
        [StringLength(150, ErrorMessage = "El email no puede superar 150 caracteres.")]
        public string? email { get; set; } = string.Empty; // NOT NULL en SQL

        // === TELÉFONOS ===
        [Required(ErrorMessage = "El teléfono principal es obligatorio.")]
        [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
        public string telefono1 { get; set; } = string.Empty; // NOT NULL en SQL

        [StringLength(30)]
        public string? telefono2 { get; set; }

        // === DIRECCIÓN ===
        [Required(ErrorMessage = "La calle es obligatoria.")]        
        [StringLength(150, ErrorMessage = "La calle no puede superar 150 caracteres.")]
        public string calle { get; set; }

        [Required(ErrorMessage = "La altura es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "La altura debe ser mayor a 0.")]
        public int altura { get; set; }

        [StringLength(10)]
        public string? departamento { get; set; }

        [Required(ErrorMessage = "Seleccionar una provincia es obligatorio.")]
        public int id_provincia { get; set; }

        [Required(ErrorMessage = "Seleccionar una localidad es obligatorio.")]
        public int id_localidad { get; set; }

        // === OTROS DATOS ===
        [StringLength(200, ErrorMessage = "Las redes sociales no pueden superar 200 caracteres.")]
        public string? redesSociales { get; set; }

        [Required] // si permitís null en BD, podés quitarlo
        public int id_estadoPension { get; set; }

        [Required] // si permitís null en BD, podés quitarlo
        public int id_usuario { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime fechaIngreso { get; set; } = DateTime.Now; // DEFAULT GETDATE()

        [DataType(DataType.DateTime)]
        public DateTime? fechaEgreso { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        [Range(0, 9999999999.99, ErrorMessage = "El monto debe tener hasta 10 dígitos enteros y 2 decimales.")]
        public decimal? montoDia { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? fechaEliminacion { get; set; }

    }
}
