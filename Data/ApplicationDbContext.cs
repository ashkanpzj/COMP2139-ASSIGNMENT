using System;
using System.Linq;
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

        public override int SaveChanges()
        {
            NormalizeDateTimesToUtc();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeDateTimesToUtc();
            return base.SaveChangesAsync(cancellationToken);
        }

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

        private void NormalizeDateTimesToUtc()
        {
            foreach (var entry in ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.ClrType == typeof(DateTime) && property.CurrentValue is DateTime dt)
                    {
                        property.CurrentValue = Normalize(dt);
                    }
                    else if (property.Metadata.ClrType == typeof(DateTime?) && property.CurrentValue is DateTime ndt)
                    {
                        property.CurrentValue = Normalize(ndt);
                    }
                }
            }

            static DateTime Normalize(DateTime value) =>
                value.Kind switch
                {
                    DateTimeKind.Utc => value,
                    DateTimeKind.Local => value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
                };
        }
    }
}