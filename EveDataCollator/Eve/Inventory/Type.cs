using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve.Inventory
{
    public class Type
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;


        public int GroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;


        public int MarketGroupID { get; set; }

        public string MarketGroupName { get; set; } = string.Empty;

        public string FullMarketGroupName { get; set; } = string.Empty;


        public double BasePrice { get; set; }


        public int IconId { get; set; }


        public double Volume { get; set; }

        public double PackagedVolume { get; set; }

        public bool Published { get; set; }


    }
}
