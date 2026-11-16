using Celd.Domain;
using Xunit;
namespace Celd.Tests;

public class ReservationDomainTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Reservation New() => Reservation.Create(1, 10, Now);

    [Fact] public void Flujo_legal_Reserved_CheckedOut_Returned_deja_historial()
    {
        var r = New(); r.Pickup(99, Now); r.Return(99, Now);
        Assert.Equal(Status.Returned, r.Status);
        Assert.Equal([Status.Reserved, Status.CheckedOut, Status.Returned], r.History.Select(h => h.Status));
    }

    [Fact] public void Cancelar_desde_Reserved_es_legal()
    {
        var r = New(); r.Cancel(10, Now);
        Assert.Equal(Status.Cancelled, r.Status);
    }

    [Fact] public void Transiciones_ilegales_se_rechazan_sin_cambiar_estado_ni_historial()
    {
        var r = New(); r.Pickup(99, Now);
        Assert.Throws<InvalidTransitionException>(() => r.Cancel(10, Now));   // CheckedOut → Cancelled
        Assert.Throws<InvalidTransitionException>(() => r.Pickup(99, Now));   // repetido
        Assert.Equal(Status.CheckedOut, r.Status);
        Assert.Equal(2, r.History.Count);
    }

    [Fact] public void Return_sin_pickup_es_ilegal() => Assert.Throws<InvalidTransitionException>(() => New().Return(99, Now));
}
