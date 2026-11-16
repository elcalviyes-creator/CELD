using Celd.Application;
using Celd.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Celd.Infrastructure;

public class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _h = new();
    public string Hash(string password) => _h.HashPassword(new object(), password);
    public bool Verify(string hash, string password) => _h.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
    {
        // Para SQL Server: UseSqlServer(...) + paquete Microsoft.EntityFrameworkCore.SqlServer
        s.AddDbContext<CeldDbContext>(o => o.UseSqlite(cfg.GetConnectionString("Default") ?? "Data Source=celd.db"));
        s.AddScoped<IUnitOfWork, EfUnitOfWork>();
        s.AddScoped<IItemRepository, ItemRepository>();
        s.AddScoped<IReservationRepository, ReservationRepository>();
        s.AddScoped<IUserRepository, UserRepository>();
        s.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        return s;
    }

    public static void SeedDatabase(this IServiceProvider sp, IConfiguration cfg)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CeldDbContext>();
        db.Database.EnsureCreated();   // para el curso: reemplazar por migraciones
        if (db.Items.Any()) return;
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        // La contraseña NO va en el código (DR-002 / sección 2.3)
        var pwd = cfg["Seed:Password"] ?? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9));
        if (cfg["Seed:Password"] is null) Console.WriteLine($"[seed] Contraseña generada para alice, bob y sam: {pwd}");
        User U(string user, string name, string role) => new() { Username = user, DisplayName = name, Role = role, PasswordHash = hasher.Hash(pwd) };
        db.Users.AddRange(U("alice", "Alice (Student)", Roles.Student), U("bob", "Bob (Student)", Roles.Student), U("prof", "Professor Rivera", Roles.Faculty), U("sam", "Sam (Staff)", Roles.Staff));
        db.Items.AddRange(
            new Item { AssetCode = "LAP-001", Name = "Laptop Dell 14", Category = "Laptop", Description = "Laptop para trabajos de clase." },
            new Item { AssetCode = "LAP-002", Name = "Laptop Lenovo 15", Category = "Laptop", Description = "Laptop con 16 GB RAM." },
            new Item { AssetCode = "CAM-001", Name = "Cámara Canon", Category = "Cámara", Description = "Cámara DSLR con trípode." },
            new Item { AssetCode = "PRJ-001", Name = "Proyector Epson", Category = "Proyector", Description = "Proyector HDMI portátil." },
            new Item { AssetCode = "MIC-001", Name = "Micrófono USB", Category = "Audio", Description = "Micrófono para grabaciones." },
            new Item { AssetCode = "ARD-001", Name = "Kit Arduino", Category = "Electrónica", Description = "Kit con sensores y cables." },
            new Item { AssetCode = "TAB-001", Name = "Tablet Samsung Galaxy Tab", Category = "Tablet", Brand = "Samsung", Model = "Galaxy Tab", Description = "Tablet para uso académico." },
            new Item { AssetCode = "CAL-001", Name = "Calculadora gráfica TI", Category = "Calculadora", Brand = "Texas Instruments", Description = "Calculadora gráfica." },
            new Item { AssetCode = "KEY-001", Name = "Teclado inalámbrico", Category = "Accesorio", Description = "Teclado USB inalámbrico." },
            new Item { AssetCode = "CHG-001", Name = "Cargador USB-C", Category = "Accesorio", Description = "Cargador universal USB-C." });
        db.SaveChanges();
    }
}
