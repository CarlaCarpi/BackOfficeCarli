using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Persona : IValidatableObject
    {
        [Key]
        public int id_persona { get; set; }

        // === DATOS PERSONALES ===
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(50, ErrorMessage = "El apellido no puede superar 50 caracteres.")]
        public string apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [Range(1000000, 99999999, ErrorMessage = "Ingrese un DNI válido (entre 1.000.000 y 99.999.999).")]
        public int dni { get; set; }

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime? fechaNacimiento { get; set; }

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un email válido.")]
        [StringLength(150, ErrorMessage = "El email no puede superar 150 caracteres.")]
        public string email { get; set; } = string.Empty;

        // === TELÉFONOS ===
        [Required(ErrorMessage = "El teléfono principal es obligatorio.")]
        [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
        public string telefono1 { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
        public string? telefono2 { get; set; }

        // === DIRECCIÓN ===
        [StringLength(100, ErrorMessage = "La calle no puede superar 100 caracteres.")]
        public string? calle { get; set; }

        public int? altura { get; set; }

        [StringLength(10, ErrorMessage = "El departamento no puede superar 10 caracteres.")]
        public string? departamento { get; set; }

        [Required(ErrorMessage = "Seleccionar una provincia es obligatorio.")]
        public int? id_provincia { get; set; }

        [Required(ErrorMessage = "Seleccionar una localidad es obligatorio.")]
        public int? id_localidad { get; set; }

        // === OTROS DATOS ===
        [StringLength(200, ErrorMessage = "Las redes sociales no pueden superar 200 caracteres.")]
        public string? redesSociales { get; set; }

        [Required(ErrorMessage = "El estado de la persona es obligatorio.")]
        public int? id_estadoPersona { get; set; }

        // 👇 Nuevo campo para auditoría: quién cambió el estado
        public int? id_usuario { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime fechaIngreso { get; set; } = DateTime.Now;

        [DataType(DataType.DateTime)]
        public DateTime? fechaEgreso { get; set; }

        [StringLength(255, ErrorMessage = "Las observaciones no pueden superar 255 caracteres.")]
        public string? motivoEgreso { get; set; }

        // ==========================================
        // ✅ VALIDACIÓN PERSONALIZADA DE FECHAS
        // ==========================================
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (fechaNacimiento.HasValue)
            {
                // ❌ Fecha futura
                if (fechaNacimiento.Value.Date > DateTime.Today)
                {
                    yield return new ValidationResult(
                        "La fecha de nacimiento no puede ser futura.",
                        new[] { nameof(fechaNacimiento) }
                    );
                }

                // ❌ Menor de edad (menos de 18 años)
                var hace18 = DateTime.Today.AddYears(-18);
                if (fechaNacimiento.Value.Date > hace18)
                {
                    yield return new ValidationResult(
                        "La persona debe ser mayor de 18 años.",
                        new[] { nameof(fechaNacimiento) }
                    );
                }
            }

        }
    }
}
