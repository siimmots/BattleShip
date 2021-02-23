using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using DAL;
using Domain;
using Domain.Enums;

namespace WebApp.Pages.Games
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["GameOptionId"] = new SelectList(_context.GameOptions, "GameOptionId", "Name");
        ViewData["PlayerAId"] = new SelectList(_context.Players, "PlayerId", "Name");
        ViewData["PlayerBId"] = new SelectList(_context.Players, "PlayerId", "Name");
        ViewData["BoatTouchOptions"] = new SelectList(new[]
        {
            EBoatsCanTouch.No,
            EBoatsCanTouch.Yes,
            EBoatsCanTouch.Corners
        });
        ViewData["BoardSizes"] = new SelectList(new[] {5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20});
        ViewData["MoveAfterHitOptions"] = new SelectList(new[]
            {ENextMoveAfterHit.SamePlayer,
                ENextMoveAfterHit.OtherPlayer}
        );
        
            return Page();
        }
        

        [BindProperty]
        public string PlayerAName { get; set; } = default!;
        [BindProperty]
        public string PlayerBName { get; set; } = default!;
        
        [BindProperty]
        public EBoatsCanTouch BoatsCanTouch { get; set; } = default!;
        
        [BindProperty]
        public ENextMoveAfterHit NextMoveAfterHit { get; set; } = default!;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public int BoardWidth { get; set; } = default!;
        
        [BindProperty]
        public int BoardHeight { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            
            var playerA = new Player()
            {
                Name = PlayerAName,
                PlayerBoardStates = new List<PlayerBoardState>(),
                PlayerBoats = new List<PlayerBoat>()
            };
            
            var playerB = new Player()
            {
                Name = PlayerBName,
                PlayerBoardStates = new List<PlayerBoardState>(),
                PlayerBoats = new List<PlayerBoat>()
            };

            Description ??= $"{PlayerAName} vs {PlayerBName}";
            
            var game = new Game()
            {
                PlayerA = playerA,
                PlayerB = playerB,
                Description = Description,
                CreatedAt = DateTime.Now,
                NextMoveByPlayer1 = true
            };
            
            var gameOption = new GameOption()
            {
                Name = $"{PlayerAName} vs {PlayerBName}",
                EBoatsCanTouch = BoatsCanTouch,
                ENextMoveAfterHit = NextMoveAfterHit,
                GameOptionBoats = new List<GameOptionBoat>(),
                Games = new List<Game>(),
                BoardWidth = BoardWidth,
                BoardHeight = BoardHeight
            };
            

            await _context.Players.AddAsync(playerA);
            await _context.Players.AddAsync(playerB);
            gameOption.Games.Add(game);
            await _context.GameOptions.AddAsync(gameOption);
            game.GameOption = gameOption;
            
            await _context.Games.AddAsync(game);

            await _context.SaveChangesAsync();
            
                        
            BoardState playerABoard = new BoardState()
            {
                Board = new BoardSquareState[BoardWidth][],
            };
            
            BoardState playerBBoard = new BoardState()
            {
                Board = new BoardSquareState[BoardWidth][]
            };
            
            var dbPlayerA = _context.Players.FirstOrDefault(o => o.PlayerId == game.PlayerAId);
            var dbPlayerB = _context.Players.FirstOrDefault(o => o.PlayerId == game.PlayerBId);
            
            var playerABoardState = new PlayerBoardState()
            {
                PlayerId = dbPlayerA!.PlayerId,
                Player = dbPlayerA,
                CreatedAt = DateTime.Now,
            };

            // Create initial empty boards -> later add the boats here
            for (var x = 0; x < BoardWidth; x++)
            {
                playerABoard.Board[x] = new BoardSquareState[BoardHeight];
                playerBBoard.Board[x] = new BoardSquareState[BoardHeight];
                for (var y = 0; y < BoardHeight; y++)
                {
                    BoardSquareState squareState = new BoardSquareState()
                    {
                        BoardSquareStateId = int.Parse($"{x}{y}"),
                        Bomb = 0
                    };

                    playerABoard.Board[x][y] = squareState;
                    playerBBoard.Board[x][y] = squareState;
                }
            }
            
            var playerBBoardState = new PlayerBoardState()
            {
                PlayerId = dbPlayerB!.PlayerId,
                Player = dbPlayerB,
                CreatedAt = DateTime.Now,
            };

            // Serialize board states to Json and save to the DB
            var jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
    
            playerABoardState.BoardState = JsonSerializer.Serialize(playerABoard, jsonOptions);
            playerBBoardState.BoardState = JsonSerializer.Serialize(playerBBoard, jsonOptions);

            game.PlayerA.PlayerBoardStates.Add(playerABoardState);
            game.PlayerB.PlayerBoardStates.Add(playerBBoardState);
            
            await _context.SaveChangesAsync();
            
            // First we add the boats !
            return RedirectToPage("../GamePlay/AddBoats", new {id = game.GameId, playerId = playerA.PlayerId, boatNr = 0});
        }
    }
}
