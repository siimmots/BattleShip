using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DAL;
using Domain;
using Domain.Enums;
using GameBrain;
using GameConsoleUI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebApp.Pages.GamePlay
{
    public class AddBoats : PageModel
    {

        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        // DB reference
        public AddBoats(ILogger<IndexModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public Game? Game { get; set; }

        [BindProperty(SupportsGet = true)] private string? Dir { get; set; }

        [BindProperty(SupportsGet = true)] public static int BoardWidth { get; set; }
        [BindProperty(SupportsGet = true)] public static int BoardHeight { get; set; }

        [BindProperty(SupportsGet = true)] public Player CurrentPlayer { get; set; } = new Player();

        [BindProperty(SupportsGet = true)] public int BoatNr { get; set; } = 0;
        private BattleShip BattleShip { get; set; } = new BattleShip(BoardWidth, BoardHeight);
        
        [BindProperty(SupportsGet = true)]
        public int? BoatId { get; set; }

        [BindProperty(SupportsGet = true)] private int? BoatsToPlaceCount { get; set; }
        
        [BindProperty(SupportsGet = true)] public List<(int x, int y)>? CurrentBoatCoordinates { get; set; }
        
        public CellState[,]? Board { get; set; }

        [BindProperty(SupportsGet = true)] public bool IsOldBoat { get; set; } = false;

        [BindProperty(SupportsGet = true)] public bool IsTouching { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(int id, int? x, int? y, int? boatNr, string? dir, int? boatId, bool? auto)
        {
            
            Dir = dir ?? "";
            BoatNr = boatNr ?? 0;
            BoatId = boatId ?? 0;
            Boat currentBoat;
            
            // Get the current game and other necessary variables
            Game = await _context.Games.Where(g => g.GameId == id).FirstOrDefaultAsync();

            var playerA = await _context.Players
                .Where(p => p.PlayerId == Game.PlayerAId)
                .Include(p => p.PlayerBoats).FirstOrDefaultAsync();
            var playerB = await _context.Players.
                Where(p => p.PlayerId == Game.PlayerBId).
                Include(p => p.PlayerBoats).FirstOrDefaultAsync();
            
            CurrentPlayer = Game.NextMoveByPlayer1 ? playerA : playerB;

            var boatBoard = new CellState[BoardWidth, BoardHeight];

            var gameOptions = await _context.GameOptions.Where(x => x.GameOptionId == Game!.GameId)
                .Include(x => x.GameOptionBoats)
                .FirstOrDefaultAsync();
            
            // set the current battleship game options from the db
            BattleShip.SetGameOptions(gameOptions.EBoatsCanTouch,gameOptions.ENextMoveAfterHit);
            
            BoardWidth = gameOptions!.BoardWidth;
            BoardHeight = gameOptions!.BoardHeight;

            Board = new CellState[BoardWidth, BoardHeight];
            
            // Boats that need to placed
            var boatArray = BattleShip.GenerateBoatList(BoardWidth,BoardHeight);
            
            // If the player chose to automatically place the boats
            if (auto == true)
            {
                var autoCoordinates = BattleShip.PlaceBoatsAuto(boatBoard, boatArray);
                
                foreach (var (boat, _) in autoCoordinates)
                {
                    var playerBoat = new PlayerBoat()
                    {
                        Boat = boat,
                        BoatId = boat.BoatId,
                        IsSunken = false,
                        Name = boat.Name,
                        Player = CurrentPlayer,
                        PlayerId = CurrentPlayer.PlayerId,
                        Size = boat.Size
                    };
                    
                    CurrentPlayer.PlayerBoats.Add(playerBoat);
                    await _context.PlayerBoats.AddAsync(playerBoat);
                    await _context.SaveChangesAsync();
                    
                    BoatNr++;
                }
            }
            
            // Amount of boats that need to be placed
            BoatsToPlaceCount = boatArray.Length;
            
            // Coordinates, where boats already exist on
            var oldCoordinates = new List<(int x, int y)>();
            
            foreach (var playerBoat in CurrentPlayer.PlayerBoats)
            {
                var boat = await _context.Boats.Where(b => b.BoatId == playerBoat.BoatId)
                    .Include(b => b.BoatCoordinates).FirstOrDefaultAsync();

                oldCoordinates.AddRange(boat.BoatCoordinates.Select(coordinate => (coordinate.PosX, coordinate.PosY)));
            }
            
            // Add old boats on the boat board
            foreach (var (posX, posY) in oldCoordinates)
            {
                boatBoard[posX, posY] = CellState.O;
            }
            
            // Place the boat and save it to the DB
            if (Dir == "place")
            {
                currentBoat = await _context.Boats.Where(b => b.BoatId == BoatId)
                    .Include(b => b.BoatCoordinates).FirstOrDefaultAsync();
                
                var playerBoat = new PlayerBoat()
                {
                    Boat = currentBoat,
                    BoatId = currentBoat.BoatId,
                    IsSunken = false,
                    Name = currentBoat.Name,
                    Player = CurrentPlayer,
                    PlayerId = CurrentPlayer.PlayerId,
                    Size = currentBoat.Size
                };

                var coordinates = new (int x, int y)[currentBoat.BoatCoordinates.Count];
                var index = 0;
                foreach (var coordinate in currentBoat.BoatCoordinates)
                {
                    coordinates[index] = (coordinate.PosX,coordinate.PosY);
                    index++;
                }

                (IsOldBoat,IsTouching) = BattleShip.PlaceABoat(coordinates, boatBoard);

                if (IsTouching || IsOldBoat)
                {
                    // Display error and refresh the current URL 
                    return RedirectToPage("../GamePlay/AddBoats", new {id = Game!.GameId, IsOldBoat, IsTouching, playerId = CurrentPlayer.PlayerId, boatNr = BoatNr, boatId = BoatId});
                }
                CurrentPlayer.PlayerBoats.Add(playerBoat);
                await _context.PlayerBoats.AddAsync(playerBoat);
                await _context.SaveChangesAsync();

                BoatId = 0;
                BoatNr++;
            }
            
            if (BoatNr == boatArray.Length)
            {
                if (CurrentPlayer.PlayerId == playerB.PlayerId){
                    await SaveBoatsToDb();
                    return RedirectToPage("../GamePlay/Index", new {id = Game.GameId});
                }
                BoatNr = 0;
                BoatId = 0;
                Game.NextMoveByPlayer1 = !Game.NextMoveByPlayer1;
                CurrentPlayer = Game.PlayerB;
                
                await _context.SaveChangesAsync();
            }
            
            
            // Display all the boats that have been placed
            oldCoordinates.Clear();
            foreach (var playerBoat in CurrentPlayer.PlayerBoats)
            {
                var boat = await _context.Boats.Where(b => b.BoatId == playerBoat.BoatId)
                    .Include(b => b.BoatCoordinates).FirstOrDefaultAsync();

                oldCoordinates.AddRange(boat.BoatCoordinates.Select(coordinate => (coordinate.PosX, coordinate.PosY)));
            }
            
            var currentBoatSize = boatArray[BoatNr];
            
            foreach (var coordinate in oldCoordinates)
            {
                Board[coordinate.x, coordinate.y] = CellState.B;
            }
            
            var startingCoordinates = BattleShip.GetStartingCoordinates(Board,BoardWidth,BoardHeight,currentBoatSize);
            
            // If there is no boatId in the URL
            if (BoatId == 0)
            {
                currentBoat = new Boat()
                {
                    Size = currentBoatSize,
                    Name = $"Boat {currentBoatSize} - {Game.Description}",
                    BoatCoordinates = new List<BoatCoordinate>()
                };
                
                await _context.Boats.AddAsync(currentBoat);
                await _context.SaveChangesAsync();
                
                foreach (var (posX, posY) in startingCoordinates)
                {
                    var boatCoordinate = new BoatCoordinate()
                    {
                        PosX = posX,
                        PosY = posY,
                        BoatId = currentBoat.BoatId
                    };
                    currentBoat.BoatCoordinates.Add(boatCoordinate);
                }
                
                await _context.SaveChangesAsync();
            }
            else
            {
                currentBoat = await _context.Boats.Where(b => b.BoatId == BoatId)
                    .Include(b => b.BoatCoordinates).FirstOrDefaultAsync();

                var index = 0;
                foreach (var coordinate in currentBoat.BoatCoordinates)
                {
                    startingCoordinates[index] = (coordinate.PosX, coordinate.PosY);
                    index++;
                }
            }
            
            var newCoordinates =
                MoveBoatCoordinates(Board, BoardWidth, BoardHeight, currentBoatSize, startingCoordinates);
            
            await UpdateBoatCoordinates(currentBoat, newCoordinates);
            
            BoatId = currentBoat.BoatId;
            
            foreach (var coordinate in currentBoat.BoatCoordinates)
            {
                Board[coordinate.PosX, coordinate.PosY] = CellState.B;
                CurrentBoatCoordinates?.Add((coordinate.PosX,coordinate.PosY)); // These are used to highlight the current boat on the board
            }
            
            return Page();
        }
        
        private async Task UpdateBoatCoordinates(Boat boat, (int x, int y)[] coordinates)
        {
            
            boat.BoatCoordinates.Clear();
            await _context.SaveChangesAsync();

            foreach (var (x, y) in coordinates)
            {
                var boatCoordinate = new BoatCoordinate()
                {
                    PosX = x,
                    PosY = y,
                    BoatId = boat.BoatId
                };
                boat.BoatCoordinates.Add(boatCoordinate);
                await _context.SaveChangesAsync();
            }

            await _context.SaveChangesAsync();
        }
        
        
        private (int x, int y)[] MoveBoatCoordinates
        (CellState[,] board, int boardWidth, int boardHeight, int boatSize, (int x, int y)[] startingCoordinates)
        {
            
            var direction = EBoatDirection.Horizontal;
            var areCoordinatesEmpty = true;

            if (startingCoordinates[0].x == startingCoordinates[boatSize - 1].x)
            {
                direction = EBoatDirection.Vertical;
            }

            switch (Dir)
            {

                // Check if below is empty
                case "rotate"
                    when direction == EBoatDirection.Horizontal
                         && startingCoordinates[0].y + boatSize - 1 < boardHeight:

                    // Check if the new coordinates are empty
                    for (var i = 0; i < boatSize; i++)
                    {
                        if (board[startingCoordinates[0].x, startingCoordinates[i].y + i] != CellState.Empty)
                        {
                            areCoordinatesEmpty = false;
                        }
                    }
                    
                    // ("Starting coordinates in method move coordinates");
                    foreach (var startingCoordinate in startingCoordinates)
                    {
                        Console.WriteLine($"{startingCoordinate.x} - {startingCoordinate.y}");
                    }

                    Console.WriteLine("Horizontal -> Vertical");
                    if (areCoordinatesEmpty)
                    {
                        for (var i = 1; i < boatSize; i++)
                        {
                            startingCoordinates[i].x = startingCoordinates[0].x;
                            startingCoordinates[i].y += i;
                        }
                    }

                    break;

                case "rotate"
                    when direction == EBoatDirection.Vertical
                         && startingCoordinates[0].x + boatSize - 1 < boardWidth:
                    Console.WriteLine("Vertical -> Horizontal");

                    // Check if the new coordinates are empty
                    for (var i = 0; i < boatSize; i++)
                    {
                        if (board[startingCoordinates[i].x + i, startingCoordinates[0].y] != CellState.Empty)
                        {
                            areCoordinatesEmpty = false;
                        }
                    }

                    if (areCoordinatesEmpty)
                    {
                        for (var i = 1; i < boatSize; i++)
                        {
                            startingCoordinates[i].x += i;
                            startingCoordinates[i].y = startingCoordinates[0].y;
                        }
                    }

                    break;

                // "Width.." and "height - 1" because array starts from 0.
                // Increase X
                case "right" when startingCoordinates[boatSize - 1].x < boardWidth - 1:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].x++;
                    }

                    break;
                // Increase Y
                case "down" when startingCoordinates[0].y < boardHeight - 1:

                    // If the boat is withing the board height
                    if (startingCoordinates[boatSize - 1].y < boardHeight - 1)
                    {
                        for (var i = 0; i < boatSize; i++)
                        {
                            startingCoordinates[i].y++;
                        }
                    }

                    break;
                // Decrease X
                case "left" when startingCoordinates[0].x > 0:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].x--;
                    }

                    break;
                // Decrease Y
                case "up" when startingCoordinates[0].y > 0:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].y--;
                    }

                    break;
            }
            return startingCoordinates;
        }
        
        
        private async Task SaveBoatsToDb()
        {
            
            for (var _ = 0; _ < 2; _++)
            {
                var player = _ == 0 ? Game!.PlayerA : Game!.PlayerB;

                player = await _context.Players.Where(p => p.PlayerId == player.PlayerId)
                    .Include(p => p.PlayerBoardStates).FirstOrDefaultAsync();
                

                PlayerBoardState playerBoardState = new PlayerBoardState()
                {
                    BoardState = "",
                    Player = player,
                    // PlayerId = player.PlayerId
                };

                BoardState playerBoard = new BoardState()
                {
                    Board = new BoardSquareState[BoardWidth][]
                };
    
                // Initialize the board
                for (var j = 0; j < BoardWidth; j++)
                {
                    playerBoard.Board[j] = new BoardSquareState[BoardHeight];
                }

                foreach (var playerBoat in player.PlayerBoats)
                {
                    var gameOptionBoat = new GameOptionBoat()
                    {
                        Amount = 1,
                        Boat = playerBoat.Boat,
                        BoatId = playerBoat.BoatId,
                        GameOption = Game.GameOption,
                        GameOptionId = Game.GameOptionId
                    };

                    var boat = await _context.Boats.Where(b => b.BoatId == playerBoat.BoatId)
                        .Include(b => b.BoatCoordinates).FirstOrDefaultAsync();


                    foreach (var coordinate in boat.BoatCoordinates)
                    {
                        var x = coordinate.PosX;
                        var y = coordinate.PosY;

                        // Add the boats on the players board
                        BoardSquareState squareState = new BoardSquareState()
                        {
                            BoardSquareStateId = int.Parse($"{x}{y}"),
                            BoatId = boat.BoatId
                        };
                        playerBoard.Board[x][y] = squareState;  
                    }
                    // Save the boats to the db
                    await _context.GameOptionBoats.AddAsync(gameOptionBoat);
                    await _context.SaveChangesAsync();
                }
                
                var jsonOptions = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };
    
                // Serialize the game board and save it
                playerBoardState.BoardState = JsonSerializer.Serialize(playerBoard, jsonOptions);
                player.PlayerBoardStates.Add(playerBoardState);
                await _context.SaveChangesAsync();
            }
        }
    }
}
