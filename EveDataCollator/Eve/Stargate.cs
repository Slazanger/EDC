using EveDataCollator.Data;

namespace EveDataCollator.Eve
{
    public class Stargate
    {
        // id
        // destination
        // position
        // - X, Y, Z
        
        public int Id { get; set; }
        public int Destination { get; set; }
        public DecVector3 Position { get; set; }
        public int TypeId { get; set; }
    }
}