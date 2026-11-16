using Celd.Domain;
using Celd.Application;
using Celd.Infrastructure;
using System.Reflection;
using Xunit;
namespace Celd.Tests;

/// <summary>Contratos de capa: fallan si alguien rompe la dirección de dependencias.</summary>
public class LayerContractTests
{
    private static string[] Refs(Assembly a) => a.GetReferencedAssemblies().Select(n => n.Name!).ToArray();

    [Fact] public void Domain_no_depende_de_ninguna_otra_capa_ni_de_EF()
    {
        var refs = Refs(typeof(Reservation).Assembly);
        Assert.DoesNotContain(refs, r => r.StartsWith("Celd.Application") || r.StartsWith("Celd.Infrastructure") || r.StartsWith("Microsoft.EntityFrameworkCore") || r.StartsWith("Microsoft.AspNetCore"));
    }

    [Fact] public void Application_solo_depende_de_Domain()
    {
        var refs = Refs(typeof(ReservationService).Assembly);
        Assert.DoesNotContain(refs, r => r.StartsWith("Celd.Infrastructure") || r.StartsWith("Microsoft.EntityFrameworkCore") || r.StartsWith("Microsoft.AspNetCore"));
    }

    [Fact] public void Infrastructure_implementa_los_contratos_del_dominio()
    {
        Assert.True(typeof(IReservationRepository).IsAssignableFrom(typeof(ReservationRepository)));
        Assert.True(typeof(IItemRepository).IsAssignableFrom(typeof(ItemRepository)));
        Assert.True(typeof(IUnitOfWork).IsAssignableFrom(typeof(EfUnitOfWork)));
        Assert.True(typeof(IPasswordHasher).IsAssignableFrom(typeof(IdentityPasswordHasher)));
    }
}
