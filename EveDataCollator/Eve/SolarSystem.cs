using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class SolarSystem
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public Star Sun { get; set; } = new ();
        public List<Planet> Planets { get; set; } = new();
        public List<Station> Stations { get; set; } = new();
    }
}
