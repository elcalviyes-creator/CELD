namespace Celd.Domain;

// Contratos de persistencia: los define el dominio, los implementa Infrastructure.
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
}

public record ItemAvailability(Item Item, bool Available);

public interface IItemRepository : IRepository<Item>
{
    Task<IReadOnlyList<ItemAvailability>> SearchAsync(string? search, string? category, CancellationToken ct = default);
    Task<ItemAvailability?> GetWithAvailabilityAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Item>> ListAllAsync(CancellationToken ct = default);
    Task<bool> AssetCodeExistsAsync(string assetCode, int? exceptId = null, CancellationToken ct = default);
    Task<bool> HasReservationsAsync(int itemId, CancellationToken ct = default);
    void Remove(Item item);
}

public interface IReservationRepository : IRepository<Reservation>
{
    Task<bool> HasActiveForItemAsync(int itemId, CancellationToken ct = default);
    Task<Reservation?> GetDetailedAsync(int id, CancellationToken ct = default);   // incluye Item, Student, History
    Task<IReadOnlyList<Reservation>> ListAsync(int? studentId, Status? status, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>Una sola llamada = una transacción lógica (BR-RES-004). Lanza ConflictException si la BD rechaza por concurrencia.</summary>
public interface IUnitOfWork { Task SaveChangesAsync(CancellationToken ct = default); }
