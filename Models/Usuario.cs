// Models/Usuario.cs (BackOffice)
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SantaRamona.Backoffice.Models
{
    public class Usuario
    {
        [Key]
        public int id_usuario { get; set; }

        // BDD: NOT NULL, VARCHAR(50)
        [Required(ErrorMessage = "La clave es obligatoria.")]
        [MaxLength(50)]
        public string clave { get; set; } = string.Empty;

        // BDD: NOT NULL, VARCHAR(150), UNIQUE
        [Required(ErrorMessage = "El email es obligatorio.")]
        [StringLength(150, ErrorMessage = "El email no puede superar 150 caracteres.")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Ingrese un email válido (debe tener un dominio con extensión, ej: @gmail.com).")]
  
        public string email { get; set; } = string.Empty;

        // BDD: NOT NULL, VARCHAR(50)
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(50)]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        public string nombre { get; set; } = string.Empty;

        // BDD: NOT NULL, VARCHAR(50)
        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [MaxLength(50)]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios.")]
        public string apellido { get; set; } = string.Empty;

        // BDD: NULL, VARCHAR(100)
        //[MaxLength(100)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(100, ErrorMessage = "La calle no puede superar 100 caracteres.")]
        public string? direccion { get; set; }

        // BDD: NULL, INT
        // Si se carga, que sea positiva. Al ser nullable, no se exige en validación.
        //[Range(1, 999999, ErrorMessage = "Ingrese una altura válida.")]
        [Range(1, int.MaxValue, ErrorMessage = "La altura debe ser mayor a 0.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? altura { get; set; }

        // BDD: NULL, VARCHAR(20)
        //[MaxLength(20)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(10, ErrorMessage = "El departamento no puede superar 10 caracteres.")]

        public string? departamento { get; set; }

        // BDD: NOT NULL, INT
        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
        [MinLength(8, ErrorMessage = "El teléfono debe tener al menos 8 caracteres.")]
        public int telefono { get; set; }

        // BDD: NOT NULL, DATETIME (tiene DEFAULT GETDATE())
        // No marco Required para permitir que la API use su default si corresponde.
        public DateTime fechaAlta { get; set; }

        // BDD: NOT NULL (FK)
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un estado válido.")]
        [Required(ErrorMessage = "El estado es obligatorio.")]
        public int id_estadoUsuario { get; set; }

        // Auxiliar para el combo de rol en Create/Edit (la tabla USUARIO no lo tiene).
        //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Required(ErrorMessage = "El rol es obligatorio.")]
        public int? id_rol { get; set; }

        // Navegación/auxiliares del BackOffice
        public Estado_Usuario? Estado_Usuario { get; set; }
        public ICollection<Usuario_Rol>? UsuarioRoles { get; set; }
    }

    public class Estado_Usuario
    {
        [Key]
        public int id_estadoUsuario { get; set; }

        [Required]
        [MaxLength(50)]
        public string descripcion { get; set; } = string.Empty;
    }

    public class Rol
    {
        [Key]
        public int id_rol { get; set; }

        [Required]
        [MaxLength(100)]
        public string descripcion { get; set; } = string.Empty;
    }

    public class Usuario_Rol
    {
        public int id_usuario { get; set; }
        public int id_rol { get; set; }
    }
}
