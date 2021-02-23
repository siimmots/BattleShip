using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DAL;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        public IndexModel(ILogger<IndexModel> logger,AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public IList<Game> Game { get; set; } = default!;
        
        [BindProperty]
        public string PlayerAName { get; set; } = default!;
        [BindProperty]
        public string PlayerBName { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // Remove the games without boats
            foreach (var game in _context.Games)
            {
                var gameOptionBoats = await _context.GameOptionBoats.FirstOrDefaultAsync(x => x.GameOptionId == game.GameId);
                if (gameOptionBoats == null) _context.Games.Remove(game);
            }

            await _context.SaveChangesAsync();
            
            Game = await _context.Games
                .Include(g => g.GameOption)
                .Include(g => g.PlayerA)
                .Include(g => g.PlayerB).OrderByDescending(x => x.CreatedAt).ToListAsync();
            
        }
        
        public async Task<IActionResult> OnPostAsync()
        {
            var playerA = new Player()
            {
                Name = PlayerAName,
                PlayerBoardStates = new List<PlayerBoardState>()
            };
            
            var playerB = new Player()
            {
                Name = PlayerBName,
                PlayerBoardStates = new List<PlayerBoardState>()
            };
            
            var game = new Game()
            {
                PlayerA = playerA,
                PlayerB = playerB,
                Description = $"{PlayerAName} vs {PlayerBName}",
                CreatedAt = DateTime.Now
            };
            
            await _context.Games.AddAsync(game);
            await _context.SaveChangesAsync();

            
            return RedirectToPage("./GamePlay/Index", new {id = game.GameId});
        }
        
    }
}