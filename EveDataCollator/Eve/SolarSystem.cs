﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class SolarSystem
    {
        // border
        // center
        //      - X,Y,Z
        // corridor
        // fringe
        // hub
        // international
        // luminosity
        // max
        //      - X,Y,Z
        // min
        //      - X,Y,Z
        // planets
        // radius
        // regional
        // security
        // solarSystemID
        // solarSystemNameID
        // star
        // stargates
        // sunTypeID
        // wormholeClassID
        
        public int Id { get; set; } // Todo: Id or SolarSystemId?
        public string Name { get; set; } = String.Empty; // Obsolete, handled by NameId?
        public bool Border { get; set; }
        public DecVector3 Center { get; set; }
        public bool Corridor { get; set; }
        public List<int> DisallowedAnchorCategories { get; set; } = new();
        public bool Fringe { get; set; }
        public bool Hub { get; set; }
        public bool International { get; set; }
        public float Luminosity { get; set; }
        public DecVector3 Max { get; set; }
        public DecVector3 Min { get; set; }
        public List<Planet> Planets { get; set; } = new();
        public decimal Radius { get; set; }
        public bool Regional { get; set; }
        public float Security { get; set; }
        public int SolarSystemId { get; set; }
        public int SolarSystemNameId { get; set; }
        public Star Star { get; set; } = new ();
        public List<Stargate> Stargates { get; set; } = new();
        public List<Station> Stations { get; set; } = new();
        public int SunTypeId { get; set; }
        public int WormholeClassId { get; set; }
    }
}
