using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Domain
{
    public class GameOptionBoat
    {
        public int GameOptionBoatId { get; set; }
        
        [Range(1, Int32.MaxValue)]
        public int Amount { get; set; }
        
        public int BoatId { get; set; }
        public Boat Boat { get; set; }  = null!;
        public int GameOptionId { get; set; }
        public GameOption GameOption { get; set; } = null!;
    }
}