using MapaMensal.Models;
using Microsoft.EntityFrameworkCore;

namespace MapaMensal.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<ContaPessoal> ContasPessoais => Set<ContaPessoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkDay>()
            .HasIndex(w => new { w.ProjectId, w.Date })
            .IsUnique();

        modelBuilder.Entity<Holiday>()
            .HasIndex(h => h.Date);

        modelBuilder.Entity<AppConfig>()
            .HasIndex(c => c.Key)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Configuração IVA
        modelBuilder.Entity<AppConfig>().HasData(
            new AppConfig { Id = 1, Key = "IvaRate", Value = "0.23" }
        );

        // Projetos (dados do Excel)
        modelBuilder.Entity<Project>().HasData(
            new Project { Id = 1, Name = "KCSIT/FNAC", DailyRate = 220m, IsActive = true, SortOrder = 1 },
            new Project { Id = 2, Name = "CLOSER/NB", DailyRate = 242m, IsActive = true, SortOrder = 2 },
            new Project { Id = 3, Name = "Capgemini/DPD", DailyRate = 204m, IsActive = true, SortOrder = 3 }
        );

        // Feriados Portugal 2026 (do Excel)
        modelBuilder.Entity<Holiday>().HasData(
            new Holiday { Id = 1, Date = new DateOnly(2026, 6, 4), Name = "Corpo de Deus", IsNational = true },
            new Holiday { Id = 2, Date = new DateOnly(2026, 6, 10), Name = "Dia de Portugal", IsNational = true },
            new Holiday { Id = 3, Date = new DateOnly(2026, 8, 15), Name = "Assunção de Nossa Senhora", IsNational = true },
            new Holiday { Id = 4, Date = new DateOnly(2026, 10, 5), Name = "Implantação da República", IsNational = true },
            new Holiday { Id = 5, Date = new DateOnly(2026, 11, 1), Name = "Todos os Santos", IsNational = true },
            new Holiday { Id = 6, Date = new DateOnly(2026, 12, 1), Name = "Restauração da Independência", IsNational = true },
            new Holiday { Id = 7, Date = new DateOnly(2026, 12, 8), Name = "Imaculada Conceição", IsNational = true },
            new Holiday { Id = 8, Date = new DateOnly(2026, 12, 25), Name = "Natal", IsNational = true }
        );
    }
}
