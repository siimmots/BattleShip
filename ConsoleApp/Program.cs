using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DAL;
using GameBrain;
using GameConsoleUI;
using MenuSystem;
using Microsoft.EntityFrameworkCore;
using Domain;
using Domain.Enums;

namespace ConsoleApp
{
    class Program
    {
        
        private static readonly DbContextOptionsBuilder<AppDbContext> DbOptions =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"
                    Server=barrel.itcollege.ee,1533;
                    User Id=student;
                    Password=Student.Bad.password.0;
                    Database=simots_battleshipDb;
                    MultipleActiveResultSets=true;"
                );

        private static readonly AppDbContext DbContext = new AppDbContext(DbOptions.Options);

        private static void Main(string[] args)
        {
            GoToMainMenu();
        }

        private static string DefaultMenuAction()
        {
            ConsoleMessage("This option is not implemented yet!",null,true);

            return "This option is not implemented yet!";
        }

        private static Game CreateInitialDbSave()
        {
            Console.CursorVisible = true;
            Console.WriteLine("");
            ConsoleMessage("Enter Player A's name, you can leave it empty!",ConsoleColor.Cyan,false);

            var playerAName = Console.ReadLine();

            var playerA = new Player()
            {
                Name = playerAName,
                PlayerBoardStates = new List<PlayerBoardState>()
            };
            Console.WriteLine("");
            ConsoleMessage("Enter Player B's name, you can leave it empty!",ConsoleColor.Cyan,false);

            var playerBName = Console.ReadLine();

            var playerB = new Player()
            {
                Name = playerBName,
                PlayerBoardStates = new List<PlayerBoardState>()
            };
            var dbGame = new Game()
            {
                Description = $"{playerAName} vs {playerBName}"
            };

            dbGame.PlayerA = playerA;
            dbGame.PlayerB = playerB;
            
            EBoatsCanTouch[] touchOptions = {EBoatsCanTouch.No, EBoatsCanTouch.Yes, EBoatsCanTouch.Corners};
            string[] touchLabels = {"Can't touch", "Boats can touch in any way", "Only the corners can touch"};

            var canTouch = ChooseGameOption(touchOptions, touchLabels, "Can the boats touch each other?");
            
            ENextMoveAfterHit[] nextMoveOptions = {ENextMoveAfterHit.SamePlayer, ENextMoveAfterHit.OtherPlayer};
            string[] hitLabels = {"The same player has another move", "The opponent has the next move"};

            var nextMoveAfterHit = ChooseGameOption(nextMoveOptions, hitLabels,"Who has the next move after hitting a boat?");
            
            ECustomBoatSizes[] boatOptions = {ECustomBoatSizes.No, ECustomBoatSizes.Yes};
            string[] boatLabels = {"No", "Yes"};

            var customBoats = ChooseGameOption(boatOptions, boatLabels, "Do you wish to choose your own boat sizes?");
            
            var (width, height) = GetBoardSize();

            Console.WriteLine("Applying options...");

            var gameOption = new GameOption()
            {
                Name = $"{playerAName} vs {playerBName}",
                EBoatsCanTouch = canTouch,
                ENextMoveAfterHit = nextMoveAfterHit,
                GameOptionBoats = new List<GameOptionBoat>(),
                Games = new List<Game>(),
                BoardHeight = height,
                BoardWidth = width,
                ECustomBoatSizes = customBoats
            };

            DbContext.Players.Add(playerA);
            DbContext.Players.Add(playerB);
            gameOption.Games.Add(dbGame);
            DbContext.GameOptions.Add(gameOption);
            dbGame.GameOption = gameOption;
            DbContext.Games.Add(dbGame);
            
            DbContext.SaveChanges();
            return dbGame;
        }

        private static T ChooseGameOption<T>(IReadOnlyList<T> options, IReadOnlyList<string> labels, string title)
        {
            var optionLabel = "";
            var optionMenu = new Menu(MenuLevel.LevelCustom, "<======== CHOOSE YOUR GAME OPTIONS ========>" +
                                                             Environment.NewLine +
                                                             $"| {title} |" +
                                                             Environment.NewLine);
            foreach (var label in labels)
            {
                optionMenu.AddMenuItem(new MenuItem($"{label}", $"{label}", () =>
                {
                    optionLabel= label;
                    return "r";
                }));
            }
            optionMenu.RunMenu(null);

            var index = labels.ToList().FindIndex(o => o == optionLabel);
            return options[index];
        }

        private static string BattleShip(BattleShip? savedGame, Game? savedDbGame)
        {
            BattleShip? game;
            Game dbGame;

            if (savedGame == null && savedDbGame == null)
            {
                dbGame = CreateInitialDbSave();
                game = new BattleShip(dbGame.GameOption.BoardWidth, dbGame.GameOption.BoardHeight);

                Console.WriteLine("Loading....");

                DbContext.SaveChanges();

                BattleShipConsoleUI.DrawBoard(game.GetBoard(game.Player1Board));
            }
            else
            {
                game = savedGame;
                dbGame = savedDbGame!;
                Console.WriteLine("Loading....");
                BattleShipConsoleUI.DrawBoard(game!.GetBoard(game.Player1Board));
            }

            game.SetGameOptions(dbGame.GameOption.EBoatsCanTouch, dbGame.GameOption.ENextMoveAfterHit);

            // Empty boards for the players to place bombs on
            var player1BombBoard = new CellState[game.Width, game.Height];
            var player2BombBoard = new CellState[game.Width, game.Height];

            var dbGameOption = DbContext.GameOptions.FirstOrDefault(x => x.GameOptionId == dbGame.GameOptionId);

            if (dbGameOption.GameOptionBoats == null || savedDbGame == null)
            {
                var boatsToPlace = game.GenerateBoatList(game.Width, game.Height);
                // The boats for the current game are randomly generated.
                if (dbGameOption.ECustomBoatSizes == ECustomBoatSizes.Yes)
                {
                    boatsToPlace = GetCustomBoats(boatsToPlace.Sum(), dbGameOption.BoardWidth,dbGameOption.BoardHeight).ToArray();
                }
                
                // Boat coordinates with for each player 
                var boatDataArray = new (Dictionary<Boat, (int, int)[]>, Player)[2];

                // Here both of the players will place their boats
                for (var i = 0; i < 2; i++)
                {
                    var player = DbContext.Players
                        .FirstOrDefault(p =>
                            p.PlayerId == (game.NextMoveByPlayer1 ? dbGame.PlayerAId : dbGame.PlayerBId));

                    var (boatDictionary, userInput) = PlacePlayersBoats(game, dbGame, player, boatsToPlace);
                    
                    boatDataArray[i] = (boatDictionary, player);

                    // If the user wants to return to the main menu or exit the game while placing the boats
                    if (userInput == "x" || userInput == "m") return userInput;

                    // Pass the turn to the next player
                    game.ChangeNextMoveBy();
                }

                // ONLY SAVE when both of the players have placed their boats
                if (boatDataArray[1].Item1.Count == boatsToPlace.Length)
                {
                    SavePlayersBoatsToDb(dbGame, boatDataArray);
                }
            }

            else
            {
                // Fill the bomb boards with data from the db
                game.GetPlayersBombsFromDb(game.Player2Board, player1BombBoard); // Player 1 bombs from player 2s board
                game.GetPlayersBombsFromDb(game.Player1Board, player2BombBoard); // Player 2 bombs from player 1s board
            }

            var menu = new Menu(MenuLevel.Level1, "============> BATTLESHIP <============");
            menu.AddMenuItem(new MenuItem("Make a move ", "p",
                () =>
                {
                    var (playersBoard, opponentsBoard) = game.NextMoveByPlayer1
                        ? (player1BombBoard, game.Player2Board)
                        : (player2BombBoard, game.Player1Board);

                    var (x, y) = GetBombCoordinates(game, playersBoard);
                    var (correctMove,didHit,isWin) = game.MakeAMove(x, y, playersBoard, opponentsBoard,dbGame);
                    while (!correctMove)
                    {
                        
                        BattleShipConsoleUI.DrawBoard(game.NextMoveByPlayer1 ? player1BombBoard : player2BombBoard);
                        break;
                    }
                    
                    if (isWin) ConsoleMessage("Winner!", ConsoleColor.Green,true);
                    else if (didHit) ConsoleMessage("Hit!",ConsoleColor.Green,true);
                    else if (!correctMove) ConsoleMessage($"There already is a bomb at this coordinate!: {x}, {y}",null,true);
                    else ConsoleMessage("Miss!",null,true);
                    
                    if (isWin) dbGame.IsOver = true;
                    DbContext.SaveChanges();

                    SaveGameAction(game,dbGame);
                    return isWin ? "m" : "";
                }));
            
            
            menu.AddMenuItem(new MenuItem("Undo last move", "u", () =>
            {
                var playerA = DbContext.Players.Where(p => p.PlayerId == dbGame.PlayerAId)
                    .Include(p => p.PlayerBoardStates).FirstOrDefault();
                var playerB = DbContext.Players.Where(p => p.PlayerId == dbGame.PlayerBId)
                    .Include(p => p.PlayerBoardStates).FirstOrDefault();
                
                if (playerA!.PlayerBoardStates.Count == 1 && playerB!.PlayerBoardStates.Count == 1)
                {
                    ConsoleMessage("You have not made any moves yet!",null,true);
                    dbGame.NextMoveByPlayer1 = true;
                    game.NextMoveByPlayer1 = true;
                    return "";
                }
                return UndoLastMove(game, dbGame);
            }));

            menu.AddMenuItem(new MenuItem("Save game", "s", () =>
            {
                Console.WriteLine("Saving....");
                return SaveGameAction(game, dbGame);
            }));
                
            menu.AddMenuItem(new MenuItem("Load game", "l", () => LoadGameAction(game)));

            // Return the users choice
            return menu.RunMenu(null);
        }
        
        private static string UndoLastMove(BattleShip game, Game dbGame)
        {
            try
            {

                var boardStateAToUndo = DbContext.Players
                    .Where(b => b.PlayerId == dbGame.PlayerAId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefault()!.Last();
                
                var boardStateBToUndo = DbContext.Players
                    .Where(b => b.PlayerId == dbGame.PlayerBId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefault()!.Last();
                
                // Change the players turn if it was a "miss"
                dbGame.NextMoveByPlayer1 = game.LastMoveWasHit ? dbGame.NextMoveByPlayer1: !dbGame.NextMoveByPlayer1;
                
                // DbContext.Remove(boardStateToUndo);
                DbContext.Remove(boardStateAToUndo);
                DbContext.Remove(boardStateBToUndo);
                DbContext.SaveChanges();
                
                
                var player1BoardState = DbContext.Players
                    .Where(b => b.PlayerId == dbGame.PlayerAId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefault()!.Last();
            
                var player2BoardState = DbContext.Players
                    .Where(b => b.PlayerId == dbGame.PlayerBId)
                    .Select(p => p.PlayerBoardStates)
                    .FirstOrDefault()!.Last();
            
                var dbGameOption = DbContext.GameOptions
                    .Where(o => o.GameOptionId == dbGame.GameOptionId)
                    .Select(g => g)
                    .FirstOrDefault();
                
                game.SetGameStateFromDb(dbGame, player1BoardState, player2BoardState, dbGameOption);
            }
            catch (Exception)
            {
                dbGame.NextMoveByPlayer1 = true;
                game.NextMoveByPlayer1 = true;
                ConsoleMessage("You have no moves to undo!",null,true);
            }
            
            var userInput = BattleShip(game, dbGame);

            return userInput;
        }
        
        private static (Dictionary<Boat, (int, int)[]>, string) PlacePlayersBoats(BattleShip game, Game dbGame, Player player,
            int[] boats)
        {
            var boatsWithCoordinates = new Dictionary<Boat, (int, int)[]>();
            
            CellState[,] board = game.NextMoveByPlayer1 ? game.Player1Board : game.Player2Board;

            var boatMenu = new Menu(MenuLevel.LevelCustom, "============> BATTLESHIP<============" +
                                                      Environment.NewLine +
                                                      "Use the Up and Down arrow keys to navigate the Menu and press ENTER to choose the menu item" +
                                                      Environment.NewLine +
                                                      "Use the arrow keys to move around the board, SPACE BAR to rotate the boat and ENTER to place the boat" +
                                                      Environment.NewLine +
                                                      "============> Boat Placement Menu <============");

            boatMenu.AddMenuItem(new MenuItem($"{player.Name} place your boats", "b",
                () =>
                {
                    
                    // Place each of the boats
                    for (var i = 0; i < boats.Length; i++)
                    {
                        var boatSize = boats[i];
                        (int x, int y)[] coordinates = GetBoatCoordinates(game, dbGame, boatSize);
                        var (oldBoat, isTouching) = game.PlaceABoat(coordinates, board);

                        if (oldBoat || isTouching)
                        {
                            // Return to the previous boat if it was misplaced
                            if (isTouching) ConsoleMessage("Boats can not touch!",null,true);
                            else if (oldBoat) ConsoleMessage("There already is a boat at this coordinate!",null,true);
                            --i;
                        }
                        else
                        {
                            var boat = new Boat()
                            {
                                Name = $"Boat {boatSize} - {dbGame.Description}",
                                Size = boatSize,
                            };
                            boatsWithCoordinates.Add(boat, coordinates);
                        }
                    }
                    
                    return "r";
                }));

            var customMenuItems = new MenuItem[]
            {
                new ("Automatically place the boats","a", () =>
                {
                    
                    boatsWithCoordinates= game.PlaceBoatsAuto(board, boats);
                    
                    return "r";
                }),
                new ("Go to the Main Menu", "m", () => "m"),
                new ("Exit the application", "x", () => "x")
            };
            var userInput = boatMenu.RunMenu(customMenuItems);
            
            return (boatsWithCoordinates,userInput);
        }

        private static void GoToMainMenu()
        {
            var mainMenu = new Menu(MenuLevel.Level0, "============> BATTLESHIP <============" +
                                                      Environment.NewLine +
                                                      "Use the Up and Down arrow keys to navigate the Menu and press ENTER to choose the menu item" +
                                                      Environment.NewLine +
                                                      "============> Main Menu <============");

            mainMenu.AddMenuItem(new MenuItem("New game human vs human", "1", () => BattleShip(null, null)));
            mainMenu.AddMenuItem(new MenuItem("Load from save", "l", () => LoadGameAction(null)));
            
            mainMenu.RunMenu(null);
        }

        private static string GoToSecondMenu()
        {
            var secondMenu = new Menu(MenuLevel.Level1, "============> Submenu 1 <============");
            secondMenu.AddMenuItem(new MenuItem("Go to submenu 2", "s", GoToSubMenu));
            Menu.DepthCounter = 2;

            return secondMenu.RunMenu(null);
        }

        private static string GoToSubMenu()
        {
            var subMenu = new Menu(MenuLevel.Level2Plus, $"============> Submenu {Menu.DepthCounter} <============");
            subMenu.AddMenuItem(new MenuItem("Go to the next submenu", "s", GoToSubMenu));
            Menu.DepthCounter++;

            return subMenu.RunMenu(null);
        }

        private static (int x, int y) GetBombCoordinates(BattleShip game, CellState[,] board)
        {
            Console.WriteLine("Use the arrow keys and ENTER to choose a cell!");

            var playersBoard = game.NextMoveByPlayer1 ? game.Player1Board : game.Player2Board;
            BattleShipConsoleUI.DrawBoard(playersBoard);

            var userInput = BattleShipConsoleUI.PlaceABomb(board, game.NextMoveByPlayer1);

            return userInput;
        }
        
        private static List<int> GetCustomBoats(int filledSquares, int width, int height)
        {
            var boatsToPlace = new List<int>();
            var squaresToFill = filledSquares;
            
            Console.Clear();
            ConsoleMessage($"Select your boat sizes until you have no more free space, right now you have {filledSquares}",ConsoleColor.Cyan,true);
            while (squaresToFill > 0)
            {
                Console.WriteLine($"Choose a boat size which is not grater than - {squaresToFill}");
                
                try
                {
                    var userValue = int.Parse(Console.ReadLine());
                    boatsToPlace.Add(userValue);
                    if (squaresToFill - userValue < 0 || userValue > width || userValue > height)
                    {
                        ConsoleMessage("You can't choose a boat this big!",null,true);
                        return GetCustomBoats(filledSquares,width,height);
                    }
                    squaresToFill -= userValue;

                    
                }
                catch (FormatException)
                {
                    ConsoleMessage("Incorrect size format!",null, true);
                    return GetCustomBoats(filledSquares,width,height);
                }
            }
            
            return boatsToPlace;
        }

        private static (int x, int y)[] GetBoatCoordinates(BattleShip game, Game dbGame, int boatSize)
        {

            var board = game.NextMoveByPlayer1 ? game.Player1Board : game.Player2Board;
            var settings = game.BoatsCanTouch;
            var userInput = BattleShipConsoleUI.PlaceABoat(board, game.NextMoveByPlayer1, boatSize, settings);
            
            return userInput;
        }

        private static (int width, int height) GetBoardSize()
        {
            Console.Clear();
            ConsoleMessage("<======== CHOOSE YOUR GAME OPTIONS ========>",ConsoleColor.Cyan,false);
            ConsoleMessage("| Insert board dimensions |",ConsoleColor.Cyan,false);
            Console.WriteLine();
            ConsoleMessage("THE MINIMUM BOARD SIZE IS 5,5!",null,false);
            ConsoleMessage("The correct format is: width,height for example: 6,7 ",null,false);

            Console.CursorVisible = true;

            int width;
            int height;

            // Check the users input
            try
            {
                var userValue = Console.ReadLine().Split(",");
                width = int.Parse(userValue[0]);

                try
                {
                    height = int.Parse(userValue[1]);
                }
                catch (IndexOutOfRangeException)
                {
                    ConsoleMessage("Please insert two values!",null,true);
                    (width, height) = GetBoardSize();
                }
            }
            catch (FormatException)
            {
                ConsoleMessage("Incorrect size format!",null,true);
                (width, height) = GetBoardSize();
            }


            if (width <= 20 && width > 4 && height <= 20 && height > 4) return (width, height);

            // Size error messages
            ConsoleMessage(width <= 4 || height <= 4
                ? $"This board is too small {width}, {height}!"
                : $"This board is too big {width}, {height}!",null, true);


            (width, height) = GetBoardSize();


            return (width, height);
        }

        private static string SaveGameAction(BattleShip game, Game dbGame)
        {
            // Construct Player A Board state
            var player1BombCounter = 0;

            BoardState player1Board = new BoardState()
            {
                Board = new BoardSquareState[game.Width][]
            };

            var playerA = DbContext.Players.Where(o => o.PlayerId == dbGame.PlayerAId).FirstOrDefault();

            PlayerBoardState player1BoardState = new PlayerBoardState()
            {
                BoardState = "",
                Player = playerA,
                PlayerId = playerA.PlayerId
            };

            var player1DbBoardState = DbContext.Players
                .Where(b => b.PlayerId == dbGame.PlayerAId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefault()!;

            var player1DbBoard = JsonSerializer.Deserialize<BoardState>(player1DbBoardState.Last().BoardState);
            
            Console.WriteLine(player1BoardState.PlayerBoardStateId);


            for (var x = 0; x < game.Width; x++)
            {
                player1Board.Board[x] = new BoardSquareState[game.Height];
                for (var y = 0; y < game.Height; y++)
                {
                    var isBomb = game.Player1Board[x, y] == CellState.X || game.Player1Board[x, y] == CellState.M;
                    BoardSquareState squareState = new BoardSquareState()
                    {
                        BoardSquareStateId = int.Parse($"{x}{y}"),
                        Bomb = isBomb ? ++player1BombCounter : 0,
                        // BoatId = player1DbBoard.Board[x][y].BoatId
                        BoatId = player1DbBoard.Board[x][y] != null &&
                                 player1DbBoard.Board[x][y].BoatId != null ?
                            player1DbBoard.Board[x][y].BoatId : null
                    };

                    player1Board.Board[x][y] = squareState;
                }
            }

            // Construct Player B Board state
            var player2BombCounter = 0;
            BoardState player2Board = new BoardState()
            {
                Board = new BoardSquareState[game.Width][]
            };

            var playerB = DbContext.Players.Where(o => o.PlayerId == dbGame.PlayerBId).FirstOrDefault();

            PlayerBoardState player2BoardState = new PlayerBoardState()
            {
                BoardState = "",
                Player = playerB,
                PlayerId = playerB.PlayerId
            };

            var player2dbBoardState = DbContext.Players
                .Where(b => b.PlayerId == dbGame.PlayerBId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefault()!;

            var player2dbBoard = JsonSerializer.Deserialize<BoardState>(player2dbBoardState.Last().BoardState);
            
            for (var x = 0; x < game.Width; x++)
            {
                player2Board.Board[x] = new BoardSquareState[game.Height];

                for (var y = 0; y < game.Height; y++)
                {
                    var isBomb = game.Player2Board[x, y] == CellState.X || game.Player2Board[x, y] == CellState.M;
                    BoardSquareState squareState = new BoardSquareState()
                    {
                        BoardSquareStateId = int.Parse($"{x}{y}"),
                        Bomb = isBomb ? ++player2BombCounter : 0,
                        BoatId = player2dbBoard.Board[x][y] != null &&
                                 player2dbBoard.Board[x][y].BoatId != null ?
                            player2dbBoard.Board[x][y].BoatId : null
                    };

                    player2Board.Board[x][y] = squareState;
                }
            }

            Console.WriteLine("");

            // Serialize board states to Json and save to the DB
            var jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            player1BoardState.BoardState = JsonSerializer.Serialize(player1Board, jsonOptions);
            player2BoardState.BoardState = JsonSerializer.Serialize(player2Board, jsonOptions);

            dbGame.PlayerA.PlayerBoardStates.Add(player1BoardState);
            dbGame.PlayerB.PlayerBoardStates.Add(player2BoardState);
            
            DbContext.SaveChanges();

            Console.WriteLine($"'{dbGame.Description}' was saved successfully!");

            return "";
        }


        private static Game GetGameSave()
        {
            var gameSave = new Game();

            var optionMenu = new Menu(MenuLevel.LevelCustom, "<======== CHOOSE A SAVED GAME ========>");
            foreach (var dbGame in DbContext.Games)
            {
                var gameOptionBoats = DbContext.GameOptionBoats.FirstOrDefault(x => x.GameOptionId == dbGame.GameId);
                if (gameOptionBoats != null && dbGame.IsOver == false)
                {
                    optionMenu.AddMenuItem(new MenuItem($"{dbGame.GameId} - {dbGame.Description} - {dbGame.CreatedAt}",$"{dbGame.GameId}",
                        () =>
                        {
                            gameSave = dbGame;
                            return "r";
                        }));
                }
            }
            
            optionMenu.RunMenu(null);
            
            return gameSave;
        }
        
        
        private static string LoadGameAction(BattleShip? game)
        {
            // Ask the user for the game save
            Console.WriteLine("");
            Console.CursorVisible = true;
            string userInput = ""; // String value that the BattleShip function will return (Menu action)
            
            var gameSave = GetGameSave();
            
            var savedGame = new BattleShip(0, 0);
            
            var player1BoardStates = DbContext.Players
                .Where(b => b.PlayerId == gameSave.PlayerAId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefault()!;
            
            var player2BoardStates = DbContext.Players
                .Where(b => b.PlayerId == gameSave.PlayerBId)
                .Select(p => p.PlayerBoardStates)
                .FirstOrDefault()!;
            
            var dbGameOption = DbContext.GameOptions
                .Where(o => o.GameOptionId == gameSave.GameOptionId)
                .Select(g => g)
                .FirstOrDefault();


            var boardStateIndex = 0;
            var optionMenu = new Menu(MenuLevel.LevelCustom, "<======== CHOOSE A SAVE ========>");
            for (var i = 0; i < player1BoardStates.Count; i++)
            {
                var boardState = player1BoardStates.ToList()[i];
                
                optionMenu.AddMenuItem(new MenuItem($"{i + 1} - {boardState.CreatedAt}",$"{boardState.PlayerBoardStateId}",
                    () =>
                    {
                        boardStateIndex = boardState.PlayerBoardStateId;
                        return "r";
                    }));
            }
            optionMenu.RunMenu(null);
            
            // ToList because then you can find elements by index
            var player1BoardStateList = player1BoardStates.ToList();
            var player2BoardStateList = player2BoardStates.ToList();
            boardStateIndex = player1BoardStateList.FindIndex(i => i.PlayerBoardStateId == boardStateIndex);
            
            
            // Get game from the DB
            if (game == null)
            {
                // GameSave is the dbGame that has the players, their boards etc..
                game = savedGame.SetGameStateFromDb(gameSave, player1BoardStateList[boardStateIndex], player2BoardStateList[boardStateIndex], dbGameOption);
                userInput = BattleShip(game, gameSave);
            }
            else
            {
                game.SetGameStateFromDb(gameSave, player1BoardStateList[boardStateIndex], player2BoardStateList[boardStateIndex], dbGameOption);
            }
            
            return userInput;
        }

        public static void ConsoleMessage(string message, ConsoleColor? color, bool readKey)
        {
            Console.ForegroundColor = color ?? ConsoleColor.Red; // Red is the default message/error color
            Console.Error.WriteLine(message);
            
            if (readKey)
            {
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("Press ANY key to continue");
                Console.ReadKey(true);
            }
            Console.ResetColor();
        }

        
        // Create a new player board state with their boats
        private static void SavePlayersBoatsToDb(Game game, (Dictionary<Boat, (int x, int y)[]>, Player)[] boatsData)
        {
            Console.WriteLine("Saving changes...");
            foreach (var (boatDictionary, player) in boatsData)
            {
                BoardState playerBoard = new BoardState()
                {
                    Board = new BoardSquareState[game.GameOption.BoardWidth][]
                };

                // Initialize the board
                for (var i = 0; i < game.GameOption.BoardWidth; i++)
                {
                    playerBoard.Board[i] = new BoardSquareState[game.GameOption.BoardHeight];
                }

                PlayerBoardState playerBoardState = new PlayerBoardState()
                {
                    BoardState = "",
                    Player = player,
                    PlayerId = player.PlayerId
                };


                foreach (var (boat, coordinates) in boatDictionary)
                {
                    var gameOptionBoat = new GameOptionBoat()
                    {
                        Amount = 1,
                        Boat = boat,
                        GameOption = game.GameOption,
                        GameOptionId = game.GameOptionId
                    };
                    
                    DbContext.GameOptionBoats.Add(gameOptionBoat);
                    DbContext.Boats.Add(boat);

                    // Save the boats to the db
                    DbContext.SaveChanges();
                    
                    
                    for (var i = 0; i < boat.Size; i++)
                    {
                        var x = coordinates[i].x;
                        var y = coordinates[i].y;

                        // Add the boats on the players board
                        BoardSquareState squareState = new BoardSquareState()
                        {
                            BoardSquareStateId = int.Parse($"{x}{y}"),
                            BoatId = boat.BoatId
                        };

                        playerBoard.Board[x][y] = squareState;
                    }
                }

                var jsonOptions = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };

                // Serialize the game board and save it
                playerBoardState.BoardState = JsonSerializer.Serialize(playerBoard, jsonOptions);
                player.PlayerBoardStates.Add(playerBoardState);


                // Save the game boards with the  boats to the players
                DbContext.SaveChanges();
            }
        }
    }
}