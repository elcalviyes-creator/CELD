using Celd.Domain;
using Microsoft.EntityFrameworkCore;
namespace Celd.Infrastructure;

public class CeldDbContext(DbContextOptions<CeldDbContext> o) : DbContext(o)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<HistoryEvent> History => Set<HistoryEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Item>().HasIndex(i => i.AssetCode).IsUnique();
        b.Entity<Item>().HasIndex(i => i.Category);
        b.Entity<Item>().Property(i => i.Condition).HasMaxLength(20);
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Reservation>().Property(r => r.Version).IsConcurrencyToken();
        b.Entity<Reservation>().HasMany(r => r.History).WithOne().HasForeignKey(h => h.ReservationId);
        // Máximo 1 reserva activa (Reserved=0, CheckedOut=1) por equipo, garantizado por la BD (BR-RES-002)
        b.Entity<Reservation>().HasIndex(r => r.ItemId).IsUnique().HasFilter("\"Status\" IN (0, 1)");
    }
}

public class EfUnitOfWork(CeldDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        // La capa de infraestructura traduce errores de EF a excepciones del dominio: las capas superiores no conocen EF.
        catch (DbUpdateConcurrencyException) { throw new ConflictException("El recurso cambió mientras se procesaba la acción."); }
        catch (DbUpdateException) { throw new ConflictException("El equipo ya tiene una reserva activa."); }
    }
}

public class EfRepository<T>(CeldDbContext db) : IRepository<T> where T : class
{
    protected readonly CeldDbContext Db = db;
    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => await Db.Set<T>().FindAsync([id], ct);
    public virtual async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);
}

public class ItemRepository(CeldDbContext db) : EfRepository<Item>(db), IItemRepository
{
    private IQueryable<ItemAvailability> WithAvailability(IQueryable<Item> q) => q.Select(i => new ItemAvailability(i,
        !i.InMaintenance && !Db.Reservations.Any(r => r.ItemId == i.Id && (r.Status == Status.Reserved || r.Status == Status.CheckedOut))));

    public async Task<IReadOnlyList<ItemAvailability>> SearchAsync(string? search, string? category, CancellationToken ct = default)
    {
        var q = Db.Items.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => EF.Functions.Like(i.Name, $"%{search}%"));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(i => i.Category == category);
        return await WithAvailability(q.OrderBy(i => i.Name)).ToListAsync(ct);
    }

    public Task<ItemAvailability?> GetWithAvailabilityAsync(int id, CancellationToken ct = default) =>
        WithAvailability(Db.Items.AsNoTracking().Where(i => i.Id == id)).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<Item>> ListAllAsync(CancellationToken ct = default) => await Db.Items.AsNoTracking().OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync(ct);
    public Task<bool> AssetCodeExistsAsync(string assetCode, int? exceptId = null, CancellationToken ct = default) =>
        Db.Items.AnyAsync(i => i.AssetCode == assetCode && (exceptId == null || i.Id != exceptId), ct);
    public Task<bool> HasReservationsAsync(int itemId, CancellationToken ct = default) => Db.Reservations.AnyAsync(r => r.ItemId == itemId, ct);
    public void Remove(Item item) => Db.Items.Remove(item);
}

public class ReservationRepository(CeldDbContext db) : EfRepository<Reservation>(db), IReservationRepository
{
    public Task<bool> HasActiveForItemAsync(int itemId, CancellationToken ct = default) =>
        Db.Reservations.AnyAsync(r => r.ItemId == itemId && (r.Status == Status.Reserved || r.Status == Status.CheckedOut), ct);

    public Task<Reservation?> GetDetailedAsync(int id, CancellationToken ct = default) =>
        Db.Reservations.Include(r => r.Item).Include(r => r.Student).Include(r => r.History).ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Reservation>> ListAsync(int? studentId, Status? status, CancellationToken ct = default)
    {
        var q = Db.Reservations.Include(r => r.Item).Include(r => r.Student).AsQueryable();
        if (studentId is not null) q = q.Where(r => r.StudentId == studentId);
        if (status is not null) q = q.Where(r => r.Status == status);
        return await q.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
    }
}

public class UserRepository(CeldDbContext db) : EfRepository<User>(db), IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
    public Task<int> CountAsync(CancellationToken ct = default) => Db.Users.CountAsync(ct);
}
