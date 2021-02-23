using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain
{
    public class Game
    {
        public int GameId { get; set; }

        public int GameOptionId { get; set; }
        public GameOption GameOption { get; set; } = null!;

        [MaxLength(200)]
        public string Description { get; set; } = DateTime.Now.ToLongDateString();
        
        public bool IsOver { get; set; } // Is the game over or not

        public bool NextMoveByPlayer1 { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int PlayerAId { get; set; }

        public Player PlayerA { get; set; } = null!;
        
        public int PlayerBId { get; set; }

        public Player PlayerB { get; set; } = null!;
    }
}