using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class Planet
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public int TypeId { get; set; }
        public int Workforce { get; set; }

        public List<AsteroidBelt> AsteroidBelts { get; set; } = new ();
        public List<Moon> Moons { get; set; } = new ();
    }
}
