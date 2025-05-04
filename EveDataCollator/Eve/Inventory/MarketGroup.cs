using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve.Inventory
{
    public class MarketGroup
    {
        public string Description { get; set; } = string.Empty;
        public bool HasTypes { get; set; }

        public int IcondID { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Id { get; set; }
        public int ParentGroupID { get; set; }
        public bool Published { get; set; }
    }
}
