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
    public DbSet<CategoriaContaPessoal> CategoriasContasPessoais => Set<CategoriaContaPessoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>().ToTable("mapa_projects");
        modelBuilder.Entity<WorkDay>().ToTable("mapa_workdays");
        modelBuilder.Entity<Holiday>().ToTable("mapa_holidays");
        modelBuilder.Entity<Expense>().ToTable("mapa_expenses");
        modelBuilder.Entity<AppConfig>().ToTable("mapa_appconfigs");
        modelBuilder.Entity<User>().ToTable("mapa_users");
        modelBuilder.Entity<Tarefa>().ToTable("mapa_tarefas");
        modelBuilder.Entity<ContaPessoal>().ToTable("mapa_contas_pessoais");
        modelBuilder.Entity<CategoriaContaPessoal>().ToTable("mapa_categorias_contas_pessoais");

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

        // Categorias de contas pessoais (padrão)
        modelBuilder.Entity<CategoriaContaPessoal>().HasData(
            new CategoriaContaPessoal { Id = 1, Nome = "Habitação",     Cor = "#5c6bc0", Ordem = 1 },
            new CategoriaContaPessoal { Id = 2, Nome = "Alimentação",   Cor = "#43a047", Ordem = 2 },
            new CategoriaContaPessoal { Id = 3, Nome = "Transporte",    Cor = "#fb8c00", Ordem = 3 },
            new CategoriaContaPessoal { Id = 4, Nome = "Saúde",         Cor = "#e53935", Ordem = 4 },
            new CategoriaContaPessoal { Id = 5, Nome = "Educação",      Cor = "#8e24aa", Ordem = 5 },
            new CategoriaContaPessoal { Id = 6, Nome = "Comunicações",  Cor = "#00897b", Ordem = 6 },
            new CategoriaContaPessoal { Id = 7, Nome = "Lazer",         Cor = "#f4511e", Ordem = 7 },
            new CategoriaContaPessoal { Id = 8, Nome = "Seguros",       Cor = "#039be5", Ordem = 8 },
            new CategoriaContaPessoal { Id = 9, Nome = "Assinaturas",   Cor = "#6d4c41", Ordem = 9 },
            new CategoriaContaPessoal { Id = 10, Nome = "Outros",       Cor = "#757575", Ordem = 10 }
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
