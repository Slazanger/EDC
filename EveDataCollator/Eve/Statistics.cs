using System.ComponentModel.DataAnnotations;

namespace EveDataCollator.Eve
{
    public class Statistics
    {
        public int Id { get; set; } // Auto set on Add
        public decimal Age { get; set; }
        public float Density { get; set; }
        public float Eccentricity { get; set; }
        public float EscapeVelocity { get; set; }
        public bool Fragmented { get; set; }
        public float Life { get; set; }
        public bool Locked { get; set; }
        public decimal MassDust { get; set; }
        public decimal MassGas { get; set; }
        public decimal OrbitPeriod { get; set; }
        public decimal OrbitRadius { get; set; }
        public float Pressure { get; set; }
        public decimal Radius { get; set; }
        public float RotationRate { get; set; }
        [StringLength(32)]
        public string SpectralClass { get; set; } = String.Empty;
        public float SurfaceGravity { get; set; }
        public float Temperature { get; set; }
    }   
}