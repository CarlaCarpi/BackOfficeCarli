using System.ComponentModel.DataAnnotations;

namespace SantaRamona.Backoffice.Models
{
    public class Estado_Pension
    {
        [Key]
        public int id_estadoPension { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        public string descripcion { get; set; } = string.Empty;
    }
}
