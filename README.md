# CELD — Campus Equipment Lending Desk

Aplicación web ASP.NET Core 8 con **arquitectura Layered (por capas)**. La arquitectura original se conserva y se amplía:

```
Celd.Api  →  Celd.Application  →  Celd.Domain
    └───────────────┐                 ▲
                    ▼                 │
             Celd.Infrastructure ─────┘
```

- **Domain:** entidades, estados, reglas del negocio, excepciones y contratos de repositorios.
- **Application:** casos de uso, DTOs, validaciones y autorización por rol.
- **Infrastructure:** EF Core, SQLite, repositorios, Unit of Work, seed y hashing de contraseñas.
- **Api:** endpoints HTTP, autenticación por cookies, composición de dependencias y frontend estático.
- **Tests:** pruebas de dominio, integración SQLite y contratos de arquitectura.

## Funcionalidad incluida en esta versión

- Inicio y cierre de sesión con cookies HttpOnly.
- Roles Student, Faculty, Staff y Admin (las cuentas de demostración incluyen Student, Faculty y Staff).
- Catálogo con búsqueda y disponibilidad calculada desde el servidor.
- Solicitud, cancelación, entrega y devolución de equipos.
- Inventario individual por código de activo, con categoría, marca, modelo, número de serie, ubicación, condición y estado de mantenimiento.
- CRUD de inventario protegido para Staff/Admin. No permite eliminar equipos con historial de préstamos.
- Dashboard con total de equipos, disponibles, prestados, reservados, en mantenimiento y usuarios.
- Frontend responsive en español.
- Persistencia SQLite y protección en base de datos contra dos reservas activas del mismo equipo.

## Ejecutar

Requiere .NET 8 SDK (o SDK posterior compatible).

```powershell
dotnet restore
dotnet test
dotnet run --project src/Celd.Api --Seed:Password="UnaClaveLocal123!"
```

Abrir `http://localhost:5000` o la URL que indique ASP.NET Core en la consola.

Cuentas demo (todas usan la contraseña indicada en `Seed:Password`):
- `alice` — Student
- `bob` — Student
- `prof` — Faculty
- `sam` — Staff

Si no se configura `Seed:Password`, el seed genera una contraseña aleatoria y la imprime en consola.

> El seed inicializa datos solo cuando la tabla de equipos está vacía. Para una base de datos de demostración completamente nueva, detén la aplicación y elimina `celd.db` antes de volver a iniciarla. No borres una base de datos que contenga datos que quieras conservar.

## Endpoints principales

- `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/me`
- `GET /api/items`, `GET /api/items/{id}`
- `GET /api/reservations`, `POST /api/reservations`, `POST /api/reservations/{id}/cancel`
- `POST /api/reservations/{id}/pickup`, `POST /api/reservations/{id}/return`
- `GET /api/inventory`, `POST /api/inventory`, `PUT /api/inventory/{id}`, `DELETE /api/inventory/{id}`
- `GET /api/dashboard`

## Próximas mejoras recomendadas

Esta entrega amplía el prototipo, pero no debe presentarse como sistema institucional de producción todavía. Faltan: migraciones EF Core (actualmente se usa `EnsureCreated`), vencimientos y cálculo de atrasos, reservas por rango de fechas/calendario, reportes exportables, gestión de daños y bitácora de auditoría, administración de cuentas/contraseñas, protección CSRF formal, pruebas E2E de HTTP y configuración segura de producción (HTTPS, secretos y política de cookies).
