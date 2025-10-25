// Models/Usuario.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SantaRamona.Backoffice.Models
{
    public class Usuario
    {
        [Key]
        public int id_usuario { get; set; }

        [Required(ErrorMessage = "La clave es obligatoria.")]
        public string clave { get; set; }

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string email { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        public string nombre { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios.")]
        public string apellido { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        public string direccion { get; set; }

        [Required(ErrorMessage = "La altura es obligatoria.")]
        [Range(1, 99999, ErrorMessage = "Ingrese una altura válida.")]
        public int altura { get; set; }

        public string? departamento { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        public int telefono { get; set; }
        public DateTime fechaAlta { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un estado válido.")]
        // [JsonPropertyName("id_estado")] // <-- solo si tu API recibe 'id_estado'
        public int id_estadoUsuario { get; set; }

        public Estado_Usuario? Estado_Usuario { get; set; }
        public ICollection<Usuario_Rol>? UsuarioRoles { get; set; }
        public int? id_rol { get; set; }
    }

    public class Estado_Usuario
    {
        public int id_estadoUsuario { get; set; }
        public string descripcion { get; set; }
    }

    public class Rol
    {
        public int id_rol { get; set; }
        public string descripcion { get; set; }
    }

    public class Usuario_Rol
    {
        public int id_usuario { get; set; }
        public int id_rol { get; set; }
    }
}
