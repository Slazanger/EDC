using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class SolarSystem
    {
        public string Name { get; set; }
        public int  Id { get; set; }

        List<Planet> Planets { get; set; }
    }
}
