using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class Planet
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public int TypeId { get; set; }
        public List<Moon> Moons { get; set; }
    }
}
