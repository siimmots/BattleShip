using System;
using System.ComponentModel.DataAnnotations;

namespace Domain
{
    public class PlayerBoat
    {
        public int PlayerBoatId { get; set; }

        [Range(1, Int32.MaxValue)]
        public int Size { get; set; }

        [MaxLength(32)]
        public string Name { get; set; } = null!;

        public bool IsSunken { get; set; }
        
        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;

        public int BoatId { get; set; }
        public Boat Boat { get; set; } = null!;
    }
}