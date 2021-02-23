using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Domain;
using Domain.Enums;

namespace GameBrain
{
    public class BattleShip
    {
        
        private int _width { get; set; }
        private int _height { get; set; }
        private CellState[,] _player1Board;
        private CellState[,] _player2Board;
        private bool _lastMoveWasHit;
        private EBoatsCanTouch _boatsCanTouch;
        private ENextMoveAfterHit _nextMoveAfterHit;
        private bool _nextMoveByPlayer1 { get; set; } = true;
        public BattleShip(int width, int height)
        {
            _width = width;
            _height = height;
            _player1Board = new CellState[width, height];
            _player2Board = new CellState[width, height];
        }
        
        public CellState[,] GetBoard(CellState[,] board)
        {
            var res = new CellState[Width,Height];
            Array.Copy(board, res, board.Length);
            return res;
        }

        public CellState GetCell(int x, int y,Game dbGame, CellState[,]? board)
        {
            if (board == null)
            {
                board = dbGame.NextMoveByPlayer1 ? _player1Board : _player2Board;
            }
            
            return board[x, y];
        }
        
        public bool NextMoveByPlayer1
        {
            get => _nextMoveByPlayer1;
            set => _nextMoveByPlayer1 = value;
        }

        public bool LastMoveWasHit
        {
            get => _lastMoveWasHit;
            set => _lastMoveWasHit = value;
        }

        public CellState[,] Player1Board
        {
            get => _player1Board;
        }
        
        public CellState[,] Player2Board
        {
            get => _player2Board;
        }

        public int Height => _height;

        public int Width => _width;
        
        public EBoatsCanTouch BoatsCanTouch => _boatsCanTouch;
        
        public ENextMoveAfterHit NextMoveAfterHit => _nextMoveAfterHit;

        public void SetGameOptions(EBoatsCanTouch boatsCanTouch, ENextMoveAfterHit nextMoveAfterHit)
        {
            _boatsCanTouch = boatsCanTouch;
            _nextMoveAfterHit = nextMoveAfterHit;
        }

        public (bool,bool,bool) MakeAMove(int x, int y, CellState[,] playersBoard, CellState[,] opponentsBoard, Game dbGame)
        {
            
            var didHit = false;
            var isWin = false;
            
            
            if (playersBoard[x, y] == CellState.Empty)
            {
                if (opponentsBoard[x, y] == CellState.O)
                {
                    opponentsBoard[x, y] = CellState.X; // Mark the bomb on the opponents board as well
                    playersBoard[x, y] = CellState.X; // The bomb hit
                    didHit = true;
                    isWin = CheckForWin(opponentsBoard);
                }
                else
                {
                    opponentsBoard[x, y] = CellState.M; // Mark the bomb on the opponents board as well
                    playersBoard[x, y] = CellState.M; // The bomb missed
                }

                _lastMoveWasHit = didHit;

                _nextMoveByPlayer1 = _nextMoveAfterHit == ENextMoveAfterHit.SamePlayer && didHit ? _nextMoveByPlayer1 : !_nextMoveByPlayer1;
                dbGame.NextMoveByPlayer1 = _nextMoveByPlayer1;
                return (true,didHit,isWin);
            }
            
            
            return (false,didHit,isWin);
        }
        

        public bool CheckForWin(CellState[,] opponentsBoard)
        {
            var boatCount = 0;
            for (var x = 0; x < _width; x++)
            {
                for (var y = 0; y < _height; y++)
                {
                    if (opponentsBoard[x, y] == CellState.O)
                    {
                        boatCount++;
                    }
                }
            }
            return boatCount == 0;
        }

        public void ChangeNextMoveBy()
        {
            _nextMoveByPlayer1 = !_nextMoveByPlayer1;
        }
        
        public (bool,bool) PlaceABoat((int x,int y)[] coordinates, CellState[,] board)
        {
            List<(int x, int y )> boatCoordinates = new ();
            var oldBoat = false;
            var isTouching = false;

            var direction = EBoatDirection.Horizontal;
            // If the boat is longer than 1
            if (coordinates.Length > 1)
            {
                direction = coordinates[0].x == coordinates[1].x ? EBoatDirection.Vertical : EBoatDirection.Horizontal;
            }
            
            for (var i = 0; i < coordinates.Length; i++)
            {
                
                var x = coordinates[i].x;
                var y = coordinates[i].y;
                
                // Check if the coordinates are empty
                if (board[x, y] == CellState.Empty)
                {
                    var isFirst = i == 0;
                    // Check if the boat is in a valid coordinate
                    if (_boatsCanTouch != EBoatsCanTouch.Yes)
                    {
                        if (!CheckIfValidCoordinate(x,y,isFirst,board,direction))
                        {
                            isTouching = true;
                            return (oldBoat,isTouching);
                        }
                    }
                    boatCoordinates.Add((x,y));
                    
                }
                else
                {
                    // Undo all of the current boat placement
                    foreach (var oldCoordinate in boatCoordinates)
                    {
                        board[oldCoordinate.x, oldCoordinate.y] = CellState.Empty;
                    }

                    // There already is a boat at the given coordinate
                    oldBoat = true;

                    return (oldBoat,isTouching);
                }
            }

            // If all the coordinates are OK, add them on the board
            foreach (var (x, y) in boatCoordinates)
            {
                board[x, y] = CellState.O;
            }

            return (oldBoat,isTouching);
        }
        
        public Dictionary<Boat, (int, int)[]> PlaceBoatsAuto(CellState[,] board, int[] boats)
        {

            var boatsWithCoordinates = new Dictionary<Boat, (int, int)[]>();

            var width = board.GetUpperBound(0);
            var height = board.GetUpperBound(1);
            var horizontal = true;
            var startingX = 0;
            var startingY = 0;
            for (var i = 0; i < boats.Length; i++)
            {
                var boatSize = boats[i];
                
                (int x, int y)[] coordinates = new (int x, int y)[boatSize];
                
                coordinates[0] = (startingX, startingY);
                for (var j = 1; j < boatSize; j++)
                {
                    if (horizontal)
                    {
                        coordinates[j] = (startingX + j, startingY);
                    }
                    else
                    {
                        coordinates[j] = (startingX, startingY + j);
                    }
                }

                var isCorrectPlacement = PlaceABoat(coordinates, board);
                if (isCorrectPlacement != (false, false))
                {
                    if (startingX + boatSize == width)
                    {
                        startingX = 0;
                        ++startingY;
                    }
                    else if (startingY + boatSize == height)
                    {
                        startingY = 0;
                        ++startingX;
                    }
                    else
                    {
                        ++startingX;
                    }
                    --i;
                    
                }
                else
                {
                    // Convert current boat coordinates to BoatCoordinate objects for the DB
                    ICollection<BoatCoordinate> boatCoordinates = coordinates
                        .Select(coordinate => new BoatCoordinate() {PosX = coordinate.x, PosY = coordinate.y}).ToList();
                    
                    var boat = new Boat()
                    {
                        Name = $"Boat {boatSize}",
                        Size = boatSize,
                        BoatCoordinates = boatCoordinates
                    };
                    boatsWithCoordinates.Add(boat, coordinates);
                    horizontal = !horizontal;
                    startingX = 0;
                    startingY = 0;
                }
            }
            
            Console.WriteLine("Placing the boats...");
            return boatsWithCoordinates;
        }

        
        public BattleShip SetGameStateFromDb(Game dbGame, PlayerBoardState playerABoardState,PlayerBoardState playerBBoardState, GameOption gameOption)
        {
            
            // Get player boards from the DB
            var playerABoard = JsonSerializer.Deserialize<BoardState>(playerABoardState.BoardState);
            var playerBBoard = JsonSerializer.Deserialize<BoardState>(playerBBoardState.BoardState);
            
            _height = gameOption.BoardHeight;
            _width = gameOption.BoardWidth;
            

            _nextMoveByPlayer1 = dbGame.NextMoveByPlayer1;
            _boatsCanTouch = gameOption.EBoatsCanTouch;
            _nextMoveAfterHit = gameOption.ENextMoveAfterHit;

            _player1Board = new CellState[_width,_height];
            _player2Board = new CellState[_width,_height];

            for (var x = 0; x < _width; x++)
            {
                for (var y = 0; y < _height; y++)
                {
                    
                    _player1Board[x, y] = GetCellStateFromDb(playerABoard.Board[x][y]);
                    _player2Board[x, y] = GetCellStateFromDb(playerBBoard.Board[x][y]);
                }
            }
            
            return this;
        }


        private static CellState GetCellStateFromDb(BoardSquareState squareState)
        {
            // If the game was not manually saved, there may be non-existent coordinates in the gameBoard from the DB
            if (squareState == null)
            {
                return CellState.Empty;
            }
            
            // Bombs are numbered
            if (squareState.BoatId != null && squareState.Bomb == 0)
            {
                // Represents the boat
                return CellState.O;
            }
            if (squareState.Bomb != 0 && squareState.BoatId != null)
            {
                // It was a hit
                return CellState.X;
            }
            return squareState.Bomb != 0 ? CellState.M : CellState.Empty;
        }

        public void GetPlayersBombsFromDb(CellState[,] dbBoard, CellState[,] bombBoard)
        {
            // Fill the bomb boards with data from the db
            for (var x = 0; x < dbBoard.GetUpperBound(0) + 1; x++)
            {
                for (var y = 0; y < dbBoard.GetUpperBound(1) + 1; y++)
                {
                    if (dbBoard[x, y] == CellState.X || dbBoard[x,y] == CellState.M)
                    {
                        
                        bombBoard[x, y] = dbBoard[x, y];
                    }
                }
            }
        }

        public int[] GenerateBoatList(int width, int height)
        {
            var boardArea = width * height;

            var boatCount = (int) Math.Floor((decimal) (boardArea / 10)) + 1;
            
            
            int[] boatSizes = {1, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5};

            int[] boatList = new int[boatCount];
            var index = 0;

            for (var i = 0; i < boatCount; i++)
            {
                boatList[i] = boatSizes[index];
                index++;

                // Move the index back to the boatSizes list beginning
                if (index == 11)
                {
                    index = 0;
                }
            }

            return boatList;
        }
        
        
        public static (int x, int y)[] GetStartingCoordinates(CellState[,] board, int width, int height, int count)
        {
            (int x, int y)[] coordinates = new (int, int)[count];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var counter = 0;

                    // Check if the coordinates are over the boards width
                    if (x + count > width) continue;

                    // Else 
                    for (var i = 0; i < count; i++)
                    {
                        if (board[x + i, y] == CellState.Empty)
                        {
                            coordinates[i].x = x + i;
                            coordinates[i].y = y;
                            counter++;
                        }
                    }

                    if (counter == count)
                    {
                        return coordinates;
                    }

                    coordinates = new (int, int)[count];
                }
            }

            return coordinates;
        }

        private bool CheckIfValidCoordinate(int x, int y, bool isFirst, CellState[,] board, EBoatDirection direction)
        {

            if (_boatsCanTouch != EBoatsCanTouch.Corners)
            {
                if (x > 0 && y > 0)
                {
                    // Top left
                    if (board[x - 1, y - 1] == CellState.O)
                    {
                        return false;
                    }
                }
            }

            
            if (y > 0) {
                if (isFirst && direction == EBoatDirection.Vertical || direction == EBoatDirection.Horizontal)
                {
                    // Top middle
                    if (board[x, y - 1] == CellState.O)
                    {
                        return false;
                    } 
                }
            }

            if (_boatsCanTouch != EBoatsCanTouch.Corners)
            {
                if (y > 0 && x != board.GetUpperBound(0))
                    // Top right
                    if (board[x + 1, y - 1] == CellState.O)
                    {
                        return false;
                    } 
            }
            
            
            if (x != board.GetUpperBound(0))
            {
                // Rigth
                if (board[x + 1, y] == CellState.O)
                {
                    return false;
                }
            }


            if (_boatsCanTouch != EBoatsCanTouch.Corners)
            {
                if (x != board.GetUpperBound(0) && y != board.GetUpperBound(1))
                {
                    // Bottom right
                    if (board[x + 1, y + 1] == CellState.O)
                    {
                        return false;
                    }
                } 
            }
            
            
            // Bottom middle
            if (y != board.GetUpperBound(1))
            {
                if (board[x, y + 1] == CellState.O)
                {
                    return false;
                }  
            }

            if (_boatsCanTouch != EBoatsCanTouch.Corners)
            {
                if (x > 0 && y != board.GetUpperBound(1))
                {
                    // Bottom left
                    if (board[x - 1, y + 1] == CellState.O)
                    {
                        return false;
                    }
                }
            }

            if (x > 0)
            {
                if (isFirst && direction == EBoatDirection.Horizontal || direction == EBoatDirection.Vertical)
                // Left
                if (board[x - 1, y] == CellState.O)
                {
                    return false;
                }
            }

            return true;
        }
    }
}