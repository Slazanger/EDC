using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class Region
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public int FactionID { get; set; }

        public List<Constellation> Constellations { get; set; } = new();
    }
}
