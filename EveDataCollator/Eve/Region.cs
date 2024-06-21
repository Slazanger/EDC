using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class Region
    {
        // center
        // - X,Y,Z
        // descriptionID
        // max
        // - X,Y,Z
        // min:
        // - X,Y,Z
        // nameID
        // nebula
        // regionID
        // wormholeClassID
        
        public int Id { get; set; }  // Todo: Decide if we keep Id or use RegionId.
        public string Name { get; set; } = String.Empty; // Obsolete, handled by NameId.
        public DecVector3 Center { get; set; }
        public int DescriptionId { get; set; }
        public int FactionId { get; set; }
        public DecVector3 Max { get; set; }
        public DecVector3 Min { get; set; }
        public int NameId { get; set; }
        public int Nebula { get; set; }
        public int RegionId { get; set; }
        public int WormholeClassId { get; set; }
        
        public List<Constellation> Constellations { get; set; } = new();
    }
}
