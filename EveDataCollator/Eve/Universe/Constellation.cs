using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.EVE.Universe
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
        
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public DecVector3 Center { get; set; }
        public DecVector3 Max { get; set; }
        public DecVector3 Min { get; set; }
        public int NameId { get; set; }
        public decimal Radius { get; set; } // TODO: Check for precision loss in DB!

        public List<SolarSystem> SolarSystems { get; set; } = new();
    }
}
