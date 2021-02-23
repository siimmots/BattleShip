namespace Domain
{
    public class BoardSquareState
    {
        public int BoardSquareStateId { get; set; }
        
        public int? BoatId { get; set; }
        
        public int Bomb { get; set; } // 0 - no bomb here yet, 1..X bomb placements in numbered order 
    }
}