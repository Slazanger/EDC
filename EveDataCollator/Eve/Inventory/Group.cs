using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve.Inventory
{
    public class Group
    {
        public bool Anchorable { get; set; }
        public bool Anchored { get; set; }
        public int CategoryID { get; set; }
        public bool FittableNonSingleton { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Published { get; set; }
        public bool UseBasePrice { get; set; }

        public int Id { get; set; }


    }
}
