using Celd.Domain;
using Microsoft.Extensions.DependencyInjection;
namespace Celd.Application;

public class AuthService(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<UserDto> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var u = await users.GetByUsernameAsync(req.Username ?? "", ct);
        // Mensaje genérico: no revela qué credencial falló (FR-AUT-001)
        if (u is null || !hasher.Verify(u.PasswordHash, req.Password ?? "")) throw new UnauthorizedException("Credenciales inválidas.");
        return new UserDto(u.Id, u.DisplayName, u.Role);
    }
}

public class CatalogService(IItemRepository items)
{
    public async Task<IReadOnlyList<ItemDto>> SearchAsync(string? search, string? category, CancellationToken ct = default) =>
        (await items.SearchAsync(search, category, ct))
            .Select(x => new ItemDto(x.Item.Id, x.Item.AssetCode, x.Item.Name, x.Item.Category, x.Available)).ToList();

    public async Task<ItemDetailDto> GetAsync(int id, CancellationToken ct = default)
    {
        var x = await items.GetWithAvailabilityAsync(id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        return new ItemDetailDto(x.Item.Id, x.Item.AssetCode, x.Item.Name, x.Item.Category, x.Item.Description, x.Available);
    }
}

public class ReservationService(IItemRepository items, IReservationRepository reservations, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<ReservationResult> ReserveAsync(Actor a, int itemId, CancellationToken ct = default)
    {
        RequireBorrower(a);
        _ = await items.GetByIdAsync(itemId, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        if (await reservations.HasActiveForItemAsync(itemId, ct)) throw new ConflictException("El equipo no está disponible.");
        var r = Reservation.Create(itemId, a.Id, clock.GetUtcNow().UtcDateTime);
        await reservations.AddAsync(r, ct);
        await uow.SaveChangesAsync(ct);   // si otro ganó la carrera, el índice único → ConflictException (BR-RES-002)
        return new ReservationResult(r.Id, r.Status.ToString());
    }

    public async Task<IReadOnlyList<ReservationDto>> ListAsync(Actor a, Status? status, CancellationToken ct = default)
    {
        int? owner = a.Role is Roles.Student or Roles.Faculty ? a.Id : null;       // estudiantes: solo las propias (BR-AUT-002)
        return (await reservations.ListAsync(owner, status, ct)).Select(ToDto).ToList();
    }

    public async Task<ReservationDetailDto> GetAsync(Actor a, int id, CancellationToken ct = default)
    {
        var r = await Load(a, id, ct);
        return new ReservationDetailDto(r.Id, r.Item.Name, r.Item.AssetCode, r.Student.DisplayName, r.Status.ToString(), r.CreatedAtUtc,
            r.History.OrderBy(h => h.AtUtc).ThenBy(h => h.Id).Select(h => new HistoryDto(h.Status.ToString(), h.AtUtc, h.Actor.DisplayName)).ToList());
    }

    public Task<ReservationResult> CancelAsync(Actor a, int id, CancellationToken ct = default) { RequireBorrower(a); return Apply(a, id, (r, now) => r.Cancel(a.Id, now), ct); }
    public Task<ReservationResult> PickupAsync(Actor a, int id, CancellationToken ct = default) { RequireStaff(a); return Apply(a, id, (r, now) => r.Pickup(a.Id, now), ct); }
    public Task<ReservationResult> ReturnAsync(Actor a, int id, CancellationToken ct = default) { RequireStaff(a); return Apply(a, id, (r, now) => r.Return(a.Id, now), ct); }

    private async Task<ReservationResult> Apply(Actor a, int id, Action<Reservation, DateTime> transition, CancellationToken ct)
    {
        var r = await Load(a, id, ct);
        transition(r, clock.GetUtcNow().UtcDateTime);   // el dominio decide si la transición es legal
        await uow.SaveChangesAsync(ct);                  // estado + historial juntos (BR-RES-004)
        return new ReservationResult(r.Id, r.Status.ToString());
    }

    private async Task<Reservation> Load(Actor a, int id, CancellationToken ct)
    {
        var r = await reservations.GetDetailedAsync(id, ct);
        // 404 también para reservas ajenas: no se filtra su existencia (NFR-SEC-001)
        if (r is null || ((a.Role is Roles.Student or Roles.Faculty) && r.StudentId != a.Id)) throw new NotFoundException("Reserva no encontrada.");
        return r;
    }

    private static void Require(Actor a, string role) { if (a.Role != role) throw new ForbiddenException("No tienes permiso para esta acción."); }
    private static void RequireStaff(Actor a) { if (a.Role is not (Roles.Staff or Roles.Admin)) throw new ForbiddenException("No tienes permiso para esta acción."); }
    private static void RequireBorrower(Actor a) { if (a.Role is not (Roles.Student or Roles.Faculty)) throw new ForbiddenException("Solo estudiantes y profesores pueden solicitar equipos."); }
    private static ReservationDto ToDto(Reservation r) => new(r.Id, r.Item.Name, r.Item.AssetCode, r.Student.DisplayName, r.Status.ToString(), r.CreatedAtUtc);
}

public class InventoryService(IItemRepository items, IReservationRepository reservations, IUserRepository users, IUnitOfWork uow)
{
    public async Task<IReadOnlyList<InventoryItemDto>> ListAsync(Actor actor, CancellationToken ct = default)
    {
        RequireStaff(actor);
        var all = await items.ListAllAsync(ct);
        var result = new List<InventoryItemDto>();
        foreach (var item in all)
        {
            var active = await reservations.HasActiveForItemAsync(item.Id, ct);
            result.Add(ToDto(item, !active && !item.InMaintenance));
        }
        return result;
    }

    public async Task<InventoryItemDto> CreateAsync(Actor actor, ItemWriteRequest request, CancellationToken ct = default)
    {
        RequireStaff(actor); Validate(request);
        var code = request.AssetCode.Trim().ToUpperInvariant();
        if (await items.AssetCodeExistsAsync(code, null, ct)) throw new ConflictException("Ya existe un equipo con ese código de inventario.");
        var item = new Item(); Apply(item, request); item.AssetCode = code;
        await items.AddAsync(item, ct); await uow.SaveChangesAsync(ct);
        return ToDto(item, !item.InMaintenance);
    }

    public async Task<InventoryItemDto> UpdateAsync(Actor actor, int id, ItemWriteRequest request, CancellationToken ct = default)
    {
        RequireStaff(actor); Validate(request);
        var item = await items.GetByIdAsync(id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        var code = request.AssetCode.Trim().ToUpperInvariant();
        if (await items.AssetCodeExistsAsync(code, id, ct)) throw new ConflictException("Ya existe un equipo con ese código de inventario.");
        Apply(item, request); item.AssetCode = code;
        await uow.SaveChangesAsync(ct);
        return ToDto(item, !item.InMaintenance && !await reservations.HasActiveForItemAsync(id, ct));
    }

    public async Task DeleteAsync(Actor actor, int id, CancellationToken ct = default)
    {
        RequireStaff(actor);
        var item = await items.GetByIdAsync(id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        if (await items.HasReservationsAsync(id, ct)) throw new ConflictException("Este equipo tiene historial de préstamos. No se puede eliminar; márcalo para mantenimiento o consérvalo en el inventario.");
        items.Remove(item); await uow.SaveChangesAsync(ct);
    }

    public async Task<DashboardDto> DashboardAsync(Actor actor, CancellationToken ct = default)
    {
        RequireStaff(actor);
        var all = await items.ListAllAsync(ct);
        var rs = await reservations.ListAsync(null, null, ct);
        return new DashboardDto(all.Count, all.Count(i => !i.InMaintenance && !rs.Any(r => r.ItemId == i.Id && (r.Status == Status.Reserved || r.Status == Status.CheckedOut))),
            rs.Count(r => r.Status == Status.CheckedOut), rs.Count(r => r.Status == Status.Reserved), all.Count(i => i.InMaintenance), await users.CountAsync(ct));
    }

    private static void Validate(ItemWriteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.AssetCode) || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Category))
            throw new ValidationException("Código, nombre y categoría son obligatorios.");
        if (r.AssetCode.Length > 40 || r.Name.Length > 120 || r.Category.Length > 60 || r.SerialNumber?.Length > 100)
            throw new ValidationException("Uno o más campos exceden la longitud permitida.");
        if (r.Condition is not ("New" or "Good" or "Fair" or "Damaged")) throw new ValidationException("La condición no es válida.");
    }
    private static void Apply(Item i, ItemWriteRequest r)
    {
        i.Name=r.Name.Trim(); i.Category=r.Category.Trim(); i.Description=r.Description?.Trim() ?? "";
        i.Brand=r.Brand?.Trim() ?? ""; i.Model=r.Model?.Trim() ?? ""; i.SerialNumber=r.SerialNumber?.Trim() ?? "";
        i.Location=string.IsNullOrWhiteSpace(r.Location)?"Campus Lending Desk":r.Location.Trim();
        i.Condition=r.Condition; i.InMaintenance=r.InMaintenance || r.Condition=="Damaged";
    }
    private static InventoryItemDto ToDto(Item i, bool available) => new(i.Id,i.AssetCode,i.Name,i.Category,i.Description,i.Brand,i.Model,i.SerialNumber,i.Location,i.Condition,i.InMaintenance,available);
    private static void RequireStaff(Actor a) { if (a.Role is not (Roles.Staff or Roles.Admin)) throw new ForbiddenException("Solo el personal autorizado puede administrar el inventario."); }
}

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddSingleton(TimeProvider.System);
        s.AddScoped<AuthService>(); s.AddScoped<CatalogService>(); s.AddScoped<ReservationService>(); s.AddScoped<InventoryService>();
        return s;
    }
}
