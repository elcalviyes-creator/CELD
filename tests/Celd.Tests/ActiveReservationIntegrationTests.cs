using Celd.Domain;
using Celd.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Celd.Tests;

/// <summary>Integración con persistencia real (SQLite): el índice único filtrado es el que protege BR-RES-002.</summary>
public class ActiveReservationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ActiveReservationIntegrationTests()
    {
        _conn.Open();
        using var db = Ctx(); db.Database.EnsureCreated();
        db.Users.AddRange(new User { Username = "a", DisplayName = "A" }, new User { Username = "b", DisplayName = "B" });
        db.Items.Add(new Item { AssetCode = "X-1", Name = "X", Category = "C" });
        db.SaveChanges();
    }
    private CeldDbContext Ctx() => new(new DbContextOptionsBuilder<CeldDbContext>().UseSqlite(_conn).Options);

    [Fact] public async Task Segunda_reserva_activa_del_mismo_equipo_es_rechazada_y_liberar_permite_otra()
    {
        using (var db = Ctx()) { db.Reservations.Add(Reservation.Create(1, 1, Now)); await new EfUnitOfWork(db).SaveChangesAsync(); }

        using (var db = Ctx())
        {
            db.Reservations.Add(Reservation.Create(1, 2, Now));
            await Assert.ThrowsAsync<ConflictException>(() => new EfUnitOfWork(db).SaveChangesAsync());
        }

        using (var db = Ctx()) { var r = await db.Reservations.Include(x => x.History).FirstAsync(); r.Cancel(1, Now); await new EfUnitOfWork(db).SaveChangesAsync(); }
        using (var db = Ctx()) { db.Reservations.Add(Reservation.Create(1, 2, Now)); await new EfUnitOfWork(db).SaveChangesAsync(); }   // ya no hay activa

        using var check = Ctx();
        Assert.Equal(1, await check.Reservations.CountAsync(r => r.Status == Status.Reserved));
        Assert.Equal(1, await check.Reservations.CountAsync(r => r.Status == Status.Cancelled)); // el historial se conserva
    }

    public void Dispose() => _conn.Dispose();
}
