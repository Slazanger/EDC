﻿namespace EveDataCollator.EVE.Universe
{
    public class Star
    {
        // id
        // radius
        // statistics
        // typeID

        public int Id { get; set; }
        public decimal Radius { get; set; }
        public Statistics Statistics { get; set; } = new();
        public int TypeId { get; set; }
        public int Power { get; set; }
    }
}