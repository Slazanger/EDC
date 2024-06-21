using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class Planet
    {
        // asteroidBelts
        // celestialIndex
        // planetAttributes
        // moons
        // position
        //      - X,Y,Z
        // radius
        // statistics
        // typeID
        
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public int Workforce { get; set; }
        public List<AsteroidBelt> AsteroidBelts { get; set; } = new ();
        public int CelestialIndex { get; set; }
        public PlanetAttributes PlanetAttributes { get; set; } = new();
        public List<Moon> Moons { get; set; } = new ();
        public DecVector3 Position { get; set; }
        public decimal Radius { get; set; }
        public Statistics Statistics { get; set; } = new();
        public int TypeId { get; set; }
    }
}
