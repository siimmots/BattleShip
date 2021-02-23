using System;
using System.Collections.Generic;
using System.Linq;

namespace MenuSystem
{
    public enum MenuLevel
    {
        Level0,
        Level1,
        Level2Plus,
        LevelCustom
    }

    public class Menu
    {
        private Dictionary<string, MenuItem> MenuItems { get; set; } = new Dictionary<string, MenuItem>();
        private readonly MenuLevel _menuLevel;
        private readonly string _header;
        private int _currentItem;
        private ConsoleKey _key;
        public static int DepthCounter = 2;

        public Menu(MenuLevel level, string header)
        {
            _menuLevel = level;
            _header = header;
        }

        public void AddMenuItem(MenuItem item)
        {
            if (MenuItems.Any(menuItem => menuItem.Value.Label == item.Label))
            {
                throw new Exception($"An item with the label: | {item.Label} | already exists");
            }
            
            if (item.Label.Length == 0)
            {
                throw new Exception("The menu item label can not be empty");
            }

            MenuItems.Add(item.UserChoice, item);
        }

        public string RunMenu(MenuItem[]? customMenuItems)
        {
            var userChoice = "";
            Console.CursorVisible = false;
            GetDefaultOptions(customMenuItems);

            do
            {
                Console.Clear();
                WriteLineWithColor(ConsoleColor.Cyan, _header);

                for (var i = 0; i < MenuItems.Count; i++)
                {
                    if (_currentItem == i)
                    {
                        WriteLineWithColor(ConsoleColor.DarkBlue, MenuItems.ElementAt(i).Value.Label);
                    }
                    else
                    {
                        Console.WriteLine(MenuItems.ElementAt(i).Value.Label);
                    }
                }

                _key = Console.ReadKey(true).Key;

                // Handles key presses
                userChoice = HandleUserInput(userChoice);
                

                // Checks the current loop userChoice
                if (CheckUserChoice(userChoice))
                {
                    break;
                }

                // Handles all the other userChoices, excluding "r", "m" and "x"
                if (MenuItems.TryGetValue(userChoice, out var userMenuItem))
                {
                    userChoice = userMenuItem.MethodToExecute();
                }


                // Checks the userChoice from the previous lower depth loop 
                if (CheckUserChoice(userChoice))
                {
                    break;
                }
            } while (true);

            // If "return to previous" was chosen, don't return the user choice "r"
            // Otherwise the loop will end up at the Main menu
            if (_menuLevel == MenuLevel.Level2Plus && userChoice == "r")
            {
                userChoice = "";
            }

            DepthCounter--;

            return userChoice;
        }

        private bool CheckUserChoice(string userChoice)
        {
            switch (userChoice)
            {
                case "x":
                {
                    if (_menuLevel == MenuLevel.Level0)
                    {
                        WriteLineWithColor(ConsoleColor.DarkMagenta, "Shutting down....");
                    }
                    return true;
                }
                case "m" when _menuLevel != MenuLevel.Level0:
                    return true;
                default:
                    return userChoice == "r" && _menuLevel == MenuLevel.Level2Plus || userChoice == "r" && _menuLevel == MenuLevel.LevelCustom;
            }
        }

        private static void WriteLineWithColor(ConsoleColor color, string line)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ResetColor();
        }

        private string HandleUserInput(string userChoice)
        {
            switch (_key)
            {
                case ConsoleKey.DownArrow:
                {
                    _currentItem++;
                    if (_currentItem > MenuItems.Count - 1) _currentItem = 0;
                    break;
                }
                case ConsoleKey.UpArrow:
                {
                    _currentItem--;
                    if (_currentItem < 0) _currentItem = Convert.ToInt32(MenuItems.Count - 1);
                    break;
                }
                // If a user choice is chosen with "Enter"
                case ConsoleKey.Enter:
                    userChoice = MenuItems.ElementAt(_currentItem).Value.UserChoice;
                    break;
            }

            return userChoice;
        }

        private void GetDefaultOptions(MenuItem[]? customMenuItems)
        {
            switch (_menuLevel)
            {
                case MenuLevel.LevelCustom:
                    if (customMenuItems != null)
                    {
                        foreach (var menuItem in customMenuItems)
                        {
                            AddMenuItem(menuItem);
                        }
                    }
                    break;
                case MenuLevel.Level0:
                    AddMenuItem(new MenuItem("Exit the application", "x", () => "x"));
                    break;
                case MenuLevel.Level1:
                    AddMenuItem(new MenuItem("Go to the Main Menu", "m", () => "m"));
                    AddMenuItem(new MenuItem("Exit the application", "x", () => "x"));
                    break;
                case MenuLevel.Level2Plus:
                    AddMenuItem(new MenuItem("Return to the previous submenu", "r", () => "r"));
                    AddMenuItem(new MenuItem("Go to the Main Menu", "m", () => "m"));
                    AddMenuItem(new MenuItem("Exit the application", "x", () => "x"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException("", "Unknown menu level!");
            }
        }
    }
}