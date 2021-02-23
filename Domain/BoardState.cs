using System.ComponentModel.DataAnnotations.Schema;

namespace Domain
{
    public class BoardState
    {
        public int BoardStateId { get; set; }
        
        public BoardSquareState[][] Board { get; set; } = null!; // oli [,]
    }
}