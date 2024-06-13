using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class Constellation
    {
        public string Name { get; set; } = String.Empty;
        public int Id { get; set; }

        public List<SolarSystem> SolarSystems { get; set; } = new();
    }
}
