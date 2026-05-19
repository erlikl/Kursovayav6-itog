using BackEnd.Models.Departments;
using BackEnd.Models.Employees;
using BackEnd.Models.Users;
using BackEnd.Models.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.DbContexts
{
    public class ApplicationContext : DbContext
    {
        public DbSet<User> Users { get; set; }  // Набор данных пользователей

        public DbSet<UserFile> UserFiles { get; set; }

        // Contracts
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<ContractParty> ContractParties { get; set; }
        public DbSet<ContractTemplate> ContractTemplates { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ContractHistory> ContractHistories { get; set; }

        // Переопределяем метод конфигурации подключения
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Строка подключения к PostgreSQL с явной кодировкой UTF-8
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=UserDb;Username=postgres;Password=123;Client Encoding=UTF8");
        }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Education> Educations { get; set; }
        public DbSet<WorkExperience> WorkExperiences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Contract>(b =>
            {
                b.HasOne(c => c.PartyA).WithMany().HasForeignKey(c => c.PartyAId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne(c => c.PartyB).WithMany().HasForeignKey(c => c.PartyBId).OnDelete(DeleteBehavior.SetNull);
                b.HasOne(c => c.ResponsibleEmployee).WithMany().HasForeignKey(c => c.ResponsibleEmployeeId).OnDelete(DeleteBehavior.SetNull);
                b.HasMany(c => c.Tags).WithMany(t => t.Contracts)
                    .UsingEntity<Dictionary<string, object>>("ContractTag",
                        r => r.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                        l => l.HasOne<Contract>().WithMany().HasForeignKey("ContractId").OnDelete(DeleteBehavior.Cascade));
            });

            modelBuilder.Entity<ContractHistory>(b =>
            {
                b.HasOne(h => h.Contract).WithMany(c => c.Histories).HasForeignKey(h => h.ContractId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}