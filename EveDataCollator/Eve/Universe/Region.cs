using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.EVE.Universe
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
        
        public int Id { get; set; } 
        [StringLength(128)]
        public string Name { get; set; } = string.Empty; // Obsolete, handled by NameId.
        public DecVector3 Center { get; set; }
        public int DescriptionId { get; set; }
        public int FactionId { get; set; }
        public DecVector3 Max { get; set; }
        public DecVector3 Min { get; set; }
        public int NameId { get; set; }
        public int Nebula { get; set; }
        public int WormholeClassId { get; set; }
        
        public List<Constellation> Constellations { get; set; } = new();
    }
}
