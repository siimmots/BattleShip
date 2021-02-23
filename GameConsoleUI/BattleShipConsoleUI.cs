using System;
using System.Collections.Generic;
using System.Linq;
using DAL;
using Domain;
using Domain.Enums;
using GameBrain;
using Microsoft.EntityFrameworkCore;

namespace GameConsoleUI
{
    public static class BattleShipConsoleUI
    {
        private static ConsoleKey _key; 
        public static void DrawBoard(CellState[,] board)
        {
            // add +1, since this is 0 based.
            Console.Clear();
            var width = board.GetUpperBound(0) + 1; // x
            var height = board.GetUpperBound(1) + 1; // y
            
            Console.Write(Environment.NewLine);
            
            for (var colIndex = 0; colIndex < width; colIndex++)
            { 
                Console.Write($"+---+");
            }
            Console.WriteLine();
            
            for (var rowIndex = 0; rowIndex < height; rowIndex++)
            {
                
                for (var colIndex = 0; colIndex < width; colIndex++)
                {
                    Console.Write($"| {CellString(board[colIndex, rowIndex])} |");
                }
                
                Console.WriteLine();
                for (var colIndex = 0; colIndex < width; colIndex++)
                {
                    Console.Write($"+---+");
                }
                
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        
        public static string CellString(CellState cellState)
        {
            switch (cellState)
            {
                case CellState.Empty: return " ";
                case CellState.O: return "0";
                case CellState.X: return "X";
                case CellState.B: return "B";
                case CellState.M: return "M";
            }
            return "-";
        }

        public static (int x, int y) PlaceABomb(CellState[,] board, bool nextMoveByPlayer1) 
        {
            CellState oldValue = CellState.Empty;
            var boardWidth = board.GetUpperBound(0) + 1; // x
            var boardHeight = board.GetUpperBound(1) + 1; // y

            
            var startingCoordinates = BattleShip.GetStartingCoordinates(board, boardWidth, boardHeight,1);
            
            // Because the count is "1", we will only get one pair of coordinates
            var x = startingCoordinates[0].x;
            var y = startingCoordinates[0].y;
            
            
            board[x, y] = CellState.B; // Starting cell state for the bomb
            DrawBoard(board);
            Console.WriteLine(nextMoveByPlayer1 ? "Player 1, it's your turn!" : "Player 2, it's your turn!");
            board[x, y] = CellState.Empty;
            
            do
            {
                var coordinates = MoveBombCoordinates(x, y, boardWidth, boardHeight);
                
                x = coordinates.x;
                y = coordinates.y;

                if (board[x, y] == CellState.X)
                {
                    oldValue = CellState.X;
                }
                else if (board[x, y] == CellState.M)
                {
                    oldValue = CellState.M;
                }
                else
                {
                    board[x, y] = CellState.B;
                }
                
                DrawBoard(board);
                board[x, y] = oldValue != CellState.Empty ? oldValue: CellState.Empty;
                oldValue = CellState.Empty;
                
                
            } while (_key != ConsoleKey.Enter);
            
            return (x, y);
        }
        
        private static (int x, int y) MoveBombCoordinates(int width, int height, int boardWidth, int boardHeight)
        {
            _key = Console.ReadKey(true).Key;
            
            
            switch (_key)
            {
                // "Width.." and "height - 1" because array starts from 0.
                case ConsoleKey.RightArrow when width < boardWidth - 1:
                    width++;
                    break;
                case ConsoleKey.DownArrow when height < boardHeight - 1:
                    height++;
                    break;
                case ConsoleKey.LeftArrow when width > 0:
                    width--;
                    break;
                case ConsoleKey.UpArrow when height > 0:
                    height--;
                    break;
            }

            return (width, height);
        }
        
        private static (int x, int y)[] MoveBoatCoordinates
            (CellState[,] board,int boardWidth, int boardHeight,int boatSize, (int x ,int y)[] startingCoordinates)
        {
            
            _key = Console.ReadKey(true).Key;

            var direction = EBoatDirection.Horizontal;
            var areCoordinatesEmpty = true;
            
            if (startingCoordinates[0].x == startingCoordinates[boatSize -1].x) {
                direction = EBoatDirection.Vertical;
            }
            
            switch (_key)
            {
                
                // Check if below is empty
                case ConsoleKey.Spacebar
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
                    
                    Console.WriteLine("Horizontal -> Vertical");
                    if (areCoordinatesEmpty)
                    {
                        for (var i = 1; i < boatSize; i++)
                        {
                            startingCoordinates[i].x = startingCoordinates[0].x;
                            startingCoordinates[i].y  += i ;
                        }
                    }
                    
                    break;
                
                case ConsoleKey.Spacebar
                    when direction == EBoatDirection.Vertical
                         && startingCoordinates[0].x + boatSize - 1 < boardWidth :
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
                case ConsoleKey.RightArrow when startingCoordinates[boatSize - 1].x< boardWidth - 1:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].x++;
                    }
                    break;
                // Increase Y
                case ConsoleKey.DownArrow when startingCoordinates[0].y < boardHeight - 1:
                    
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
                case ConsoleKey.LeftArrow when startingCoordinates[0].x > 0:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].x--;
                    }
                    break;
                // Decrease Y
                case ConsoleKey.UpArrow when startingCoordinates[0].y > 0:
                    for (var i = 0; i < boatSize; i++)
                    {
                        startingCoordinates[i].y--;
                    }
                    break;
            }

            return startingCoordinates;
        }

        public static (int x, int y)[] PlaceABoat(CellState[,] board, bool nextMoveByPlayer1, int boatSize, EBoatsCanTouch settings)
        {
            
            var boardWidth = board.GetUpperBound(0) + 1; // x
            var boardHeight = board.GetUpperBound(1) + 1; // y

            List<(int, int )> oldCoordinates = new ();
            
            var coordinates = BattleShip.GetStartingCoordinates(board, boardWidth, boardHeight, boatSize);
            
            
            foreach (var (x, y) in coordinates)
            {
                board[x, y] = CellState.B;
            }
            
            DrawBoard(board);
            Console.WriteLine(nextMoveByPlayer1 ? "Player 1, it's your turn!" : "Player 2, it's your turn!");
            foreach (var (newWidth, newHeight) in coordinates)
            {
                board[newWidth, newHeight] = CellState.Empty;
            }

            
            do
            {
                coordinates = MoveBoatCoordinates(board,boardWidth, boardHeight, boatSize, coordinates);

                foreach (var (newWidth, newHeight) in coordinates)
                {
                    if (board[newWidth, newHeight] == CellState.O)
                    {
                        oldCoordinates.Add((newWidth,newHeight));
                    }
                    board[newWidth, newHeight] = CellState.B;
                }
                
                DrawBoard(board);
                
                foreach (var (newWidth, newHeight) in coordinates)
                {
                    board[newWidth, newHeight] = CellState.Empty;
                }

                foreach (var (x, y) in oldCoordinates)
                {
                    board[x, y] = CellState.O;
                }
                
                
            } while (_key != ConsoleKey.Enter);
            
            return coordinates;
            
        }
    }
}