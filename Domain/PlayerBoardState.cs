using System;

namespace Domain
{
    public class PlayerBoardState
    {
        public int PlayerBoardStateId { get; set; }

        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // SERIALIZED 2 JSON
        public string BoardState { get; set; } = null!;
    }
}