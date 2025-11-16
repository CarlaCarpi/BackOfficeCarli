using SantaRamona.Backoffice.Models;
using System.Collections.Generic;



namespace SantaRamona.Backoffice.Models
{
    public class DonarViewModel
    {

        public IEnumerable<Donacion> Donaciones { get; set; } = new List<Donacion>();
        public IEnumerable<Punto_Acopio> PuntosAcopio { get; set; } = new List<Punto_Acopio>();
        public Dictionary<int, string> Provincias { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, string> Localidades { get; set; } = new Dictionary<int, string>();
    }
}
