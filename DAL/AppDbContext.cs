using System.Linq;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace DAL
{
    public class AppDbContext : DbContext
    {
        public DbSet<Boat> Boats { get; set; } = null!;
        public DbSet<Game> Games { get; set; } = null!;
        public DbSet<PlayerBoat> PlayerBoats { get; set; } = null!;
        public DbSet<GameOption> GameOptions { get; set; } = null!;
        public DbSet<GameOptionBoat> GameOptionBoats { get; set; } = null!;
        public DbSet<Player> Players { get; set; }  = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
            
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            foreach (var relationship in modelBuilder.Model
                .GetEntityTypes()
                .Where( e  => !e.IsOwned())
                .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
            
            modelBuilder
                .Entity<Player>()
                .HasOne<Game>()
                .WithOne(x => x.PlayerA)
                .HasForeignKey<Game>(x => x.PlayerAId);
            
            modelBuilder
                .Entity<Player>()
                .HasOne<Game>()
                .WithOne(x => x.PlayerB)
                .HasForeignKey<Game>(x => x.PlayerBId);
            
        }
    }
}