using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Data
{
    public class ApplicationDbContext : IdentityDbContext  
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Assignment1.Models.Event> Events { get; set; }
        public DbSet<Assignment1.Models.Ticket> Tickets { get; set; }
        public DbSet<Assignment1.Models.TicketPurchase> TicketPurchases { get; set; }

    }
}