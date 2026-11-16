namespace Celd.Domain;

public enum Status { Reserved, CheckedOut, Returned, Cancelled }
public static class Roles { public const string Student = "Student", Faculty = "Faculty", Staff = "Staff", Admin = "Admin"; }

public class Item
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Location { get; set; } = "Campus Lending Desk";
    public string Condition { get; set; } = "Good";
    public bool InMaintenance { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
}

public class HistoryEvent
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Status Status { get; set; }
    public int ActorId { get; set; }
    public User Actor { get; set; } = null!;
    public DateTime AtUtc { get; set; }
}

/// <summary>Aggregate root: las transiciones legales (BR-RES-003) viven AQUÍ, no en servicios ni controladores.</summary>
public class Reservation
{
    public int Id { get; private set; }
    public int ItemId { get; private set; }
    public Item Item { get; private set; } = null!;
    public int StudentId { get; private set; }
    public User Student { get; private set; } = null!;
    public Status Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int Version { get; private set; }           // token de concurrencia (acciones obsoletas)
    public List<HistoryEvent> History { get; private set; } = new();

    private Reservation() { }

    public static Reservation Create(int itemId, int studentId, DateTime nowUtc)
    {
        var r = new Reservation { ItemId = itemId, StudentId = studentId, Status = Status.Reserved, CreatedAtUtc = nowUtc };
        r.History.Add(new HistoryEvent { Status = Status.Reserved, ActorId = studentId, AtUtc = nowUtc });
        return r;
    }

    public void Cancel(int actorId, DateTime now) => Move(Status.Reserved, Status.Cancelled, actorId, now);
    public void Pickup(int actorId, DateTime now) => Move(Status.Reserved, Status.CheckedOut, actorId, now);
    public void Return(int actorId, DateTime now) => Move(Status.CheckedOut, Status.Returned, actorId, now);

    private void Move(Status from, Status to, int actorId, DateTime now)
    {
        if (Status != from) throw new InvalidTransitionException(Status, to);
        Status = to; Version++;
        History.Add(new HistoryEvent { Status = to, ActorId = actorId, AtUtc = now });
    }
}
