using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class Constellation
    {
        // center
        //     - X,Y,Z
        // constellationID
        // max
        //     -X,Y,Z
        // min
        //     -X,Y,Z
        // nameID
        // radius
        
        public string Name { get; set; } = String.Empty; // Obsolete, handled by NameId.
        public int Id { get; set; } // Todo: Decide if we keep Id or use ConstellationId.
        public DecVector3 Center { get; set; }
        public int ConstellationId { get; set; }
        public DecVector3 Max { get; set; }
        public DecVector3 Min { get; set; }
        public int NameId { get; set; }
        public decimal Radius { get; set; } // TODO: Check for precision loss in DB!

        public List<SolarSystem> SolarSystems { get; set; } = new();
    }
}
