using System.Security.Claims;
using Celd.Application;
using Celd.Domain;
using Celd.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication().AddInfrastructure(builder.Configuration);   // única capa que conoce a todas (composition root)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;                    // mitigación CSRF básica (NFR-SEC-002)
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // en producción: Always
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.Services.SeedDatabase(app.Configuration);

// Traduce excepciones del dominio a un formato de error consistente (FR-ERR-001)
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (DomainException ex)
    {
        var (status, code) = ex switch
        {
            ValidationException => (400, "validation"),
            UnauthorizedException => (401, "unauthorized"),
            ForbiddenException => (403, "forbidden"),
            NotFoundException => (404, "not_found"),
            _ => (409, "conflict")
        };
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = new { code, message = ex.Message } });
    }
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseAuthentication(); app.UseAuthorization();

static Actor Me(ClaimsPrincipal u) => new(int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!), u.Identity!.Name!, u.FindFirstValue(ClaimTypes.Role)!);

// ---- Auth ----
app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, HttpContext ctx) =>
{
    var u = await auth.LoginAsync(req);
    var id = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Name, u.DisplayName), new(ClaimTypes.Role, u.Role)],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(id));
    return Results.Ok(new { u.DisplayName, u.Role });
});
app.MapPost("/api/auth/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
app.MapGet("/api/me", (ClaimsPrincipal u) => { var a = Me(u); return Results.Ok(new { DisplayName = a.Name, a.Role }); }).RequireAuthorization();

// ---- Catálogo (público) ----
app.MapGet("/api/items", async (CatalogService s, string? search, string? category) => Results.Ok(await s.SearchAsync(search, category)));
app.MapGet("/api/items/{id:int}", async (int id, CatalogService s) => Results.Ok(await s.GetAsync(id)));

// ---- Reservas (los roles se validan en la API Y en el servicio de aplicación) ----
var res = app.MapGroup("/api/reservations").RequireAuthorization();
res.MapPost("/", async (ReserveBody b, ClaimsPrincipal u, ReservationService s) =>
{ var r = await s.ReserveAsync(Me(u), b.ItemId); return Results.Created($"/api/reservations/{r.Id}", r); }).RequireAuthorization(p => p.RequireRole(Roles.Student, Roles.Faculty));
res.MapGet("/", async (ClaimsPrincipal u, ReservationService s, Status? status) => Results.Ok(await s.ListAsync(Me(u), status)));
res.MapGet("/{id:int}", async (int id, ClaimsPrincipal u, ReservationService s) => Results.Ok(await s.GetAsync(Me(u), id)));
res.MapPost("/{id:int}/cancel", async (int id, ClaimsPrincipal u, ReservationService s) => Results.Ok(await s.CancelAsync(Me(u), id))).RequireAuthorization(p => p.RequireRole(Roles.Student, Roles.Faculty));
res.MapPost("/{id:int}/pickup", async (int id, ClaimsPrincipal u, ReservationService s) => Results.Ok(await s.PickupAsync(Me(u), id))).RequireAuthorization(p => p.RequireRole(Roles.Staff));
res.MapPost("/{id:int}/return", async (int id, ClaimsPrincipal u, ReservationService s) => Results.Ok(await s.ReturnAsync(Me(u), id))).RequireAuthorization(p => p.RequireRole(Roles.Staff));

// ---- Administración del inventario y dashboard (Staff/Admin) ----
var inventory = app.MapGroup("/api/inventory").RequireAuthorization(p => p.RequireRole(Roles.Staff, Roles.Admin));
inventory.MapGet("/", async (ClaimsPrincipal u, InventoryService s) => Results.Ok(await s.ListAsync(Me(u))));
inventory.MapPost("/", async (ItemWriteRequest body, ClaimsPrincipal u, InventoryService s) => Results.Created("/api/inventory", await s.CreateAsync(Me(u), body)));
inventory.MapPut("/{id:int}", async (int id, ItemWriteRequest body, ClaimsPrincipal u, InventoryService s) => Results.Ok(await s.UpdateAsync(Me(u), id, body)));
inventory.MapDelete("/{id:int}", async (int id, ClaimsPrincipal u, InventoryService s) => { await s.DeleteAsync(Me(u), id); return Results.NoContent(); });
app.MapGet("/api/dashboard", async (ClaimsPrincipal u, InventoryService s) => Results.Ok(await s.DashboardAsync(Me(u)))).RequireAuthorization(p => p.RequireRole(Roles.Staff, Roles.Admin));

app.Run();
public record ReserveBody(int ItemId);
public partial class Program;
