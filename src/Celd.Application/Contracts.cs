using Celd.Domain;
namespace Celd.Application;

public record Actor(int Id, string Name, string Role);   // identidad validada por el servidor, nunca del body
public record LoginRequest(string Username, string Password);
public record UserDto(int Id, string DisplayName, string Role);
public record ItemDto(int Id, string AssetCode, string Name, string Category, bool Available);
public record ItemDetailDto(int Id, string AssetCode, string Name, string Category, string Description, bool Available);
public record ItemWriteRequest(string AssetCode, string Name, string Category, string Description, string Brand, string Model, string SerialNumber, string Location, string Condition, bool InMaintenance);
public record InventoryItemDto(int Id, string AssetCode, string Name, string Category, string Description, string Brand, string Model, string SerialNumber, string Location, string Condition, bool InMaintenance, bool Available);
public record DashboardDto(int TotalItems, int AvailableItems, int CheckedOutItems, int ReservedItems, int MaintenanceItems, int TotalUsers);
public record HistoryDto(string Status, DateTime AtUtc, string Actor);
public record ReservationDto(int Id, string Item, string AssetCode, string Student, string Status, DateTime CreatedAtUtc);
public record ReservationDetailDto(int Id, string Item, string AssetCode, string Student, string Status, DateTime CreatedAtUtc, IReadOnlyList<HistoryDto> History);
public record ReservationResult(int Id, string Status);

public interface IPasswordHasher { string Hash(string password); bool Verify(string hash, string password); }  // lo implementa Infrastructure
