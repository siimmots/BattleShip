using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DAL;
using Domain;
using GameBrain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebApp.Pages.GamePlay
{
    public class Index : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        public Index(ILogger<IndexModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public Game? Game { get; set; }

        public GameOption? GameOptions { get; set; }

        [BindProperty(SupportsGet = true)]
        public static int BoardWidth { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public static int BoardHeight { get; set; }
        
        public BattleShip BattleShip { get; set; } = new BattleShip(BoardWidth,BoardHeight);

        public CellState[,] Player1BombBoard { get; set; } = new CellState[BoardWidth, BoardHeight];
        
        public CellState[,] Player2BombBoard { get; set; } = new CellState[BoardWidth, BoardHeight];

        [BindProperty(SupportsGet = true)] public bool? IsHit { get; set; } = null;
        [BindProperty(SupportsGet = true)] public bool? IsWin { get; set; } = null;
        
        [BindProperty(SupportsGet = true)] public bool? CanUndo { get; set; } = true;

        // from url
        public async Task<IActionResult> OnGetAsync(int id, int? x, int? y,bool? undo, bool? isHit)
        {
            
            IsHit = isHit;
            
            Game = await _context.Games.FirstOrDefaultAsync(x => x.GameId == id);
            
            GameOptions = await _context.GameOptions.FirstOrDefaultAsync(x => x.GameOptionId == Game!.GameId);
            
            BoardWidth = GameOptions.BoardWidth;
            BoardHeight = GameOptions.BoardHeight;
            BattleShip.LastMoveWasHit = IsHit == true;

            Player1BombBoard = new CellState[BoardWidth, BoardHeight];
            Player2BombBoard = new CellState[BoardWidth, BoardHeight];
            
            var player1BoardStates = await _context.Players
                .Where(b => b.PlayerId == Game.PlayerAId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefaultAsync();
            
            var player2BoardStates = await _context.Players
                .Where(b => b.PlayerId == Game.PlayerBId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefaultAsync();
            
            
            // Trigger the undo function
            if (undo == true)
            {
                // Only undo if both of the players have made any moves
                if (player1BoardStates.Count > 2 && player2BoardStates.Count > 2)
                {
                    await UndoLastMove();
                    // Reload the state query because the last ones were deleted with UNDO
                    player1BoardStates = await _context.Players
                        .Where(b => b.PlayerId == Game.PlayerAId)
                        .Select(p => p.PlayerBoardStates)
                        .FirstOrDefaultAsync();
            
                    player2BoardStates = await _context.Players
                        .Where(b => b.PlayerId == Game.PlayerBId)
                        .Select(p => p.PlayerBoardStates)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    // If there are no possible move to UNDO
                    CanUndo = false;
                    return RedirectToPage("../GamePlay/Index", new {id = Game!.GameId, CanUndo});
                }
            }

            var playerA = await _context.Players.FirstOrDefaultAsync(x => x.PlayerId == Game.PlayerAId);
            var playerB = await _context.Players.FirstOrDefaultAsync(x => x.PlayerId == Game.PlayerBId);

            if (Game != null && player2BoardStates.Count > 0 && player1BoardStates.Count > 0)
            {
                BattleShip = BattleShip.SetGameStateFromDb(Game, player1BoardStates.LastOrDefault()!, player2BoardStates.LastOrDefault()!, GameOptions);
            }
            
            BattleShip.GetPlayersBombsFromDb(BattleShip.Player2Board, Player1BombBoard);
            BattleShip.GetPlayersBombsFromDb(BattleShip.Player1Board, Player2BombBoard);
            
            var (playersBoard,opponentsBoard) = BattleShip.NextMoveByPlayer1 ?
                (Player1BombBoard, BattleShip.Player2Board) :
                (Player2BombBoard, BattleShip.Player1Board);
            
            
            // Save game action 
            if (x != null || y != null)
            {
                bool isCorrectMove;

                (isCorrectMove,IsHit, IsWin) = BattleShip.MakeAMove(x!.Value, y!.Value, playersBoard, opponentsBoard, Game!);
                Console.WriteLine(IsHit);
                
                if (isCorrectMove)
                {

                    if (IsWin == true) Game!.IsOver = true;
                    await SaveGameState(playerA,playerB,player1BoardStates,player2BoardStates);
                    return RedirectToPage("../GamePlay/Index", new {id = Game!.GameId, isHit = IsHit, IsWin});
                }
            }
            return Page();
        }
        
        //TODO Move to DAL folder
        public async Task SaveGameState(Player playerA, Player playerB, ICollection<PlayerBoardState> player1BoardStates, ICollection<PlayerBoardState> player2BoardStates)
        {
            var player1BombCounter = 0;
                
            BoardState player1Board = new BoardState()
            {
                Board = new BoardSquareState[BoardWidth][]
            };
            
            PlayerBoardState player1BoardState = new PlayerBoardState()
            {
                BoardState = "",
                Player = playerA,
                PlayerId = playerA.PlayerId
            };
            
            var player1DbBoard = JsonSerializer.Deserialize<BoardState>(player1BoardStates.LastOrDefault()!.BoardState);
            
            for (var width = 0; width < BoardWidth; width++)
            {
                player1Board.Board[width] = new BoardSquareState[BoardHeight];
                for (var height = 0; height < BoardHeight; height++)
                {
                    var isBomb = BattleShip.Player1Board[width, height] == CellState.X || BattleShip.Player1Board[width, height] == CellState.M;
                    BoardSquareState squareState = new BoardSquareState()
                    {
                        BoardSquareStateId = int.Parse($"{width}{height}"),
                        Bomb = isBomb ? ++player1BombCounter : 0,
                        BoatId = player1DbBoard?.Board[width][height] != null &&
                                 player1DbBoard.Board[width][height].BoatId != null ?
                            player1DbBoard.Board[width][height].BoatId : null
                    };

                    player1Board.Board[width][height] = squareState;
                }
            }

            // Construct Player B Board state
            var player2BombCounter = 0;
            BoardState player2Board = new BoardState()
            {
                Board = new BoardSquareState[BoardWidth][]
            };
            
            PlayerBoardState player2BoardState = new PlayerBoardState()
            {
                BoardState = "",
                Player = playerB,
                PlayerId = playerB.PlayerId
            };
            
            var playerBDbBoard = JsonSerializer.Deserialize<BoardState>(player2BoardStates.LastOrDefault()!.BoardState);
            
            for (var width = 0; width < BoardWidth; width++)
            {
                player2Board.Board[width] = new BoardSquareState[BoardHeight];

                for (var height = 0; height < BoardHeight; height++)
                {
                    var isBomb = BattleShip.Player2Board[width, height] == CellState.X || BattleShip.Player2Board[width, height] == CellState.M;
                    BoardSquareState squareState = new BoardSquareState()
                    {
                        BoardSquareStateId = int.Parse($"{width}{height}"),
                        Bomb = isBomb ? ++player2BombCounter : 0,
                        BoatId = playerBDbBoard?.Board[width][height] != null &&
                                 playerBDbBoard.Board[width][height].BoatId != null ?
                            playerBDbBoard.Board[width][height].BoatId : null
                    };

                    player2Board.Board[width][height] = squareState;
                }
            }
            
            // Serialize board states to Json and save to the DB
            var jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            player1BoardState.BoardState = JsonSerializer.Serialize(player1Board, jsonOptions);
            player2BoardState.BoardState = JsonSerializer.Serialize(player2Board, jsonOptions);

            Game!.PlayerA.PlayerBoardStates.Add(player1BoardState);
            Game!.PlayerB.PlayerBoardStates.Add(player2BoardState);

            await _context.SaveChangesAsync();
        }
        
        public async Task<IActionResult> UndoLastMove()
        {
            try
            {
                var boardStateAToUndo = await _context.Players
                    .Where(b => b.PlayerId == Game!.PlayerAId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefaultAsync()!;
                
                var boardStateBToUndo = await _context.Players
                    .Where(b => b.PlayerId == Game!.PlayerBId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefaultAsync()!;
                
                Game!.NextMoveByPlayer1 = BattleShip.LastMoveWasHit ? Game.NextMoveByPlayer1: !Game.NextMoveByPlayer1;
                
                _context.Remove(boardStateAToUndo.Last());
                _context.Remove(boardStateBToUndo.Last());
                await _context.SaveChangesAsync();
                
                var player1BoardStates = await _context.Players
                    .Where(b => b.PlayerId == Game.PlayerAId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefaultAsync()!;
            
                var player2BoardStates = await _context.Players
                    .Where(b => b.PlayerId == Game.PlayerBId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefaultAsync()!;
            
                var dbGameOption = await _context.GameOptions
                    .Where(o => o.GameOptionId == Game.GameOptionId)
                    .Select(g => g)
                    .FirstOrDefaultAsync();
                
                BattleShip.SetGameStateFromDb(Game, player1BoardStates.Last(), player2BoardStates.Last(), dbGameOption);
                
            }
            catch (Exception)
            {
                // Display alert
                CanUndo = false;
                return RedirectToPage("../GamePlay/Index", new {id = Game!.GameId, CanUndo});
            }

            return Page();
        }
    }
}