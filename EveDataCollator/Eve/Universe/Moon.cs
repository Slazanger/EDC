using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.EVE.Universe
{
    public class Moon
    {
        // planetAttributes
        // position
        //      - X,Y,Z
        // radius
        // statistics
        // typeID
        
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public PlanetAttributes PlanetAttributes { get; set; } = new();
        public DecVector3 Position { get; set; }
        public decimal Radius { get; set; }
        public Statistics Statistics { get; set; } = new();
        public int TypeId { get; set; }
    }
}
