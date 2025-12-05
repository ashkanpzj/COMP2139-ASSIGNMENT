using Assignment1.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>  
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Event> Events { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketPurchase> TicketPurchases { get; set; }
        public DbSet<EventComment> EventComments { get; set; }
        public DbSet<EventRating> EventRatings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Ensure unique rating per event per user (logged-in)
            builder.Entity<EventRating>()
                .HasIndex(r => new { r.EventId, r.UserId })
                .IsUnique()
                .HasFilter("\"UserId\" IS NOT NULL");

            // Ensure unique rating per event per guest email
            builder.Entity<EventRating>()
                .HasIndex(r => new { r.EventId, r.GuestEmail })
                .IsUnique()
                .HasFilter("\"GuestEmail\" IS NOT NULL");
        }
    }
}