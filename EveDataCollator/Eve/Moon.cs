using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class Moon
    {
        // planetAttributes
        // position
        //      - X,Y,Z
        // radius
        // statistics
        // typeID
        
        public string Name { get; set; } = String.Empty;
        public int Id { get; set; }
        public PlanetAttributes PlanetAttributes { get; set; } = new();
        public DecVector3 Position { get; set; }
        public decimal Radius { get; set; }
        public Statistics Statistics { get; set; } = new();
        public int TypeId { get; set; }
    }
}
