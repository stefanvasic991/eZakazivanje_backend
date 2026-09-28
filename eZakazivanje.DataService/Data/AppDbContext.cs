using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using eZakazivanje.Entity.DbSet;
using System.Threading.Tasks;

namespace eZakazivanje.DataService.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){}

        public virtual DbSet<Bussiness> Businesses { get; set; }
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Appointment> Appointments { get; set; }
        public virtual DbSet<Service> Services { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Comment> Comments { get; set; }
        public DbSet<FreeTimeSlot> FreeTimeSlots { get; set; }
        public DbSet<BusinessSchedule> BusinessSchedules { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); 

            modelBuilder.Entity<Employee>()
            .HasOne(e => e.Business)
            .WithMany(b => b.Employees)
            .HasForeignKey(e => e.BusinessId);

            // Configure ApplicationUser relationships correctly
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Appointments)
                .WithOne(a => a.ApplicationUser)
                .HasForeignKey(a => a.ApplicationUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Remove the duplicate User navigation property
            modelBuilder.Entity<Appointment>()
                .Ignore("User");

            // Add unique constraint to prevent double bookings
            // This ensures no overlapping appointments for the same employee on the same date
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.EmployeeId, a.AppointmentDate, a.StartTime, a.EndTime })
                .IsUnique()
                .HasFilter("\"IsCancelled\" = false"); // Only apply constraint to non-cancelled appointments

            modelBuilder.Entity<Address>()
                .HasOne(a => a.ApplicationUser)
                .WithMany()
                .HasForeignKey(a => a.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ApplicationUser)
                .WithMany()
                .HasForeignKey(c => c.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Remove old User references
            modelBuilder.Entity<Address>().Ignore("User");
            modelBuilder.Entity<Comment>().Ignore("User");

            // Configure Business to User relationship (many businesses per user)
            modelBuilder.Entity<Bussiness>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Subscription relationships
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Business)
                .WithMany(b => b.Subscriptions)
                .HasForeignKey(s => s.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.SubscriptionPlan)
                .WithMany(sp => sp.Subscriptions)
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
