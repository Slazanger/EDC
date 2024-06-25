namespace EveDataCollator.Eve
{
    public class PlanetAttributes
    {
        public int Id { get; set; } // Auto set on Add
        public int HeightMap1 { get; set; }
        public int HeightMap2 { get; set; }
        public bool Population { get; set; }
        public int ShaderPreset { get; set; }
    }
}