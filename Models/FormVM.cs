using SantaRamona.Backoffice.Models;

namespace SantaRamona.Backoffice.Models
{    
    public class FormVM
    {
        public int IdPersona { get; set; }
        public List<Pregunta> Preguntas { get; set; }
        public Dictionary<int, string> Respuestas { get; set; } = new();
    }
}
