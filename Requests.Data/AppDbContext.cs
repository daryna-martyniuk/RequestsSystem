using Microsoft.EntityFrameworkCore;
using Requests.Data.Models;

namespace Requests.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<User> Users { get; set; }

        public DbSet<RequestStatus> RequestStatuses { get; set; }
        public DbSet<RequestPriority> RequestPriorities { get; set; }
        public DbSet<RequestCategory> RequestCategories { get; set; }

        public DbSet<Request> Requests { get; set; }
        public DbSet<RequestComment> RequestComments { get; set; }
        public DbSet<RequestAttachment> RequestAttachments { get; set; }

        public DbSet<DepartmentTask> DepartmentTasks { get; set; }
        public DbSet<TaskExecutor> TaskExecutors { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var cnstring = "Server=.;Database=RequestsSystemDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
                optionsBuilder.UseSqlServer(cnstring);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Якщо видаляємо Юзера, не видаляємо його запити 
            modelBuilder.Entity<Request>()
                .HasOne(r => r.Author)
                .WithMany()
                .HasForeignKey(r => r.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Коментарі залишаються, навіть якщо юзер видалений
            modelBuilder.Entity<RequestComment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Виконавці задач
            modelBuilder.Entity<TaskExecutor>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Логи
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Видаляти задачу можна тільки разом із запитом 
            // але НЕ треба видаляти задачу, якщо видаляється відділ або статус 
            modelBuilder.Entity<DepartmentTask>()
                .HasOne(dt => dt.Department)
                .WithMany()
                .HasForeignKey(dt => dt.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<DepartmentTask>()
                .HasOne(dt => dt.Status)
                .WithMany()
                .HasForeignKey(dt => dt.StatusId)
                .OnDelete(DeleteBehavior.Restrict); 

            // Також варто захистити сам Request від видалення довідників
            modelBuilder.Entity<Request>()
                .HasOne(r => r.GlobalStatus)
                .WithMany()
                .HasForeignKey(r => r.GlobalStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Priority)
                .WithMany()
                .HasForeignKey(r => r.PriorityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Category)
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}