# TechStore

Tienda de tecnología hecha con **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core** y **SQL Server**, con inicio de sesión, roles y protección de rutas usando **ASP.NET Core Identity**.

Proyecto de la materia Programación Web II (Frontend) - UEES.

---

## Contenido

1. [Requisitos](#1-requisitos)
2. [Levantar el proyecto por primera vez](#2-levantar-el-proyecto-por-primera-vez)
3. [Si ya tenías el proyecto antes de Identity](#3-si-ya-tenías-el-proyecto-antes-de-identity)
4. [Usuarios, roles y permisos](#4-usuarios-roles-y-permisos)
5. [Cómo proteger pantallas nuevas](#5-cómo-proteger-pantallas-nuevas)
6. [Migraciones: reglas para trabajar en equipo](#6-migraciones-reglas-para-trabajar-en-equipo)
7. [Estructura del proyecto](#7-estructura-del-proyecto)
8. [Flujo de trabajo con Git](#8-flujo-de-trabajo-con-git)
9. [Pruebas automatizadas](#9-pruebas-automatizadas)
10. [Problemas comunes](#10-problemas-comunes)

---

## 1. Requisitos

| Herramienta | Para qué | Cómo comprobarlo |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Compilar y ejecutar | `dotnet --version` |
| Herramienta `dotnet-ef` | Aplicar y crear migraciones | `dotnet ef --version` |
| SQL Server (Docker **o** SQL Server Express) | Base de datos | — |

Si no tienes `dotnet-ef`:

```bash
dotnet tool install --global dotnet-ef
```

> Los comandos `dotnet` de las secciones 2 a 6 se ejecutan dentro de la carpeta del proyecto: **`TechStore/TechStore`** (donde está `TechStore.csproj`). Los de la [sección 9 (Pruebas automatizadas)](#9-pruebas-automatizadas) se ejecutan desde la raíz de la solución, donde está `TechStore.slnx`.

---

## 2. Levantar el proyecto por primera vez

### Paso 1. Tener SQL Server encendido

**Opción A — Docker (recomendada).** Crea el contenedor una sola vez (cambia `TuClaveSA123!` por una clave tuya):

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=TuClaveSA123!" -e "MSSQL_PID=Express" \
  -p 1433:1433 --name techstore-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

Las siguientes veces solo enciéndelo:

```bash
docker start techstore-sql
```

**Opción B — SQL Server Express instalado en Windows.** Asegúrate de que la instancia `SQLEXPRESS` esté corriendo.

### Paso 2. Crear el usuario de base de datos `techstore_user`

La aplicación se conecta con el usuario `techstore_user`, que se crea con el script [`create_user.sql`](create_user.sql) (le da permiso para crear la base de datos).

- **Docker:**
  ```bash
  docker cp create_user.sql techstore-sql:/tmp/create_user.sql
  docker exec -it techstore-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "TuClaveSA123!" -C -i /tmp/create_user.sql
  ```
- **SQL Server Express:** abre `create_user.sql` en SSMS o Azure Data Studio y ejecútalo.

### Paso 3. Configurar tus *user secrets*

Los datos sensibles o que cambian según la máquina **no van en el repositorio**: se guardan en los *user secrets* de cada uno (quedan en tu usuario de Windows, fuera del proyecto).

Desde `TechStore/TechStore`:

```bash
# Usuario Administrador que se crea automáticamente al arrancar la app (obligatorio para entrar como Administrador)
dotnet user-secrets set "AdminInicial:Usuario" "admin"
dotnet user-secrets set "AdminInicial:Email" "admin@techstore.com"
dotnet user-secrets set "AdminInicial:Password" "Admin123!"
```

Si usas **Docker**, configura también la cadena de conexión (en `appsettings.json` está la de SQL Server Express):

```bash
dotnet user-secrets set "ConnectionStrings:TechStoreDB" "Server=localhost,1433;Database=TechStoreDB;User Id=techstore_user;Password=TechStore123!;TrustServerCertificate=True;"
```

Para revisar lo que tienes configurado: `dotnet user-secrets list`.

> La contraseña debe cumplir las reglas de Identity: mínimo 6 caracteres, con mayúscula, minúscula, número y símbolo. Si no cumple, la app arranca pero no crea el Administrador (aparece el error en la consola).

### Paso 4. Crear la base de datos (aplicar migraciones)

```bash
dotnet ef database update
```

Esto crea la base `TechStoreDB` con las tablas de productos, categorías y las de Identity (`AspNetUsers`, `AspNetRoles`, ...), además de los productos y categorías iniciales.

### Paso 5. Ejecutar

```bash
dotnet run --launch-profile http
```

Abre **http://localhost:5238** (con `--launch-profile https` es https://localhost:7060).

Al arrancar, la aplicación crea automáticamente (con `RoleManager` y `UserManager`) los roles **Administrador**, **Vendedor** y **Cliente**, y el usuario Administrador de tus *user secrets* si todavía no existe.

---

## 3. Si ya tenías el proyecto antes de Identity

Si ya tenías la base de datos creada con las migraciones anteriores, solo necesitas:

```bash
git checkout master
git pull
cd TechStore
dotnet restore                 # descarga el paquete nuevo de Identity
dotnet user-secrets set "AdminInicial:Usuario" "admin"
dotnet user-secrets set "AdminInicial:Email" "admin@techstore.com"
dotnet user-secrets set "AdminInicial:Password" "Admin123!"
dotnet ef database update      # aplica la migración AgregarIdentity (no borra tus datos)
dotnet run --launch-profile http
```

La migración `AgregarIdentity` **solo agrega** las tablas de usuarios y roles; no modifica ni borra productos ni categorías.

---

## 4. Usuarios, roles y permisos

### Roles

Definidos según el enunciado del EP3:

| Rol | Quién lo tiene | Qué puede hacer |
|---|---|---|
| **Administrador** | El usuario de `AdminInicial` (y a quien otro Administrador se lo asigne) | Acceso completo: consultar, agregar, editar y **eliminar** productos, generar reportes y administrar usuarios |
| **Vendedor** | Lo asigna el Administrador | Consultar, agregar y editar productos, y generar reportes (**no** puede eliminar) |
| **Cliente** | Todo usuario que se registra desde la página | Únicamente consultar los productos |

Los roles son fijos; no se crean ni eliminan desde la interfaz.

### Qué pantalla requiere qué

| Pantalla / acción | Visitante | Cliente | Vendedor | Administrador |
|---|:-:|:-:|:-:|:-:|
| Inicio, Contacto, Acerca de | ✅ | ✅ | ✅ | ✅ |
| Productos (listado y detalle), Categorías | ❌ (va al login) | ✅ | ✅ | ✅ |
| Agregar / Editar productos | ❌ (va al login) | ❌ (acceso denegado) | ✅ | ✅ |
| Eliminar productos | ❌ (va al login) | ❌ (acceso denegado) | ❌ (acceso denegado) | ✅ |
| Reportes PDF / Excel (pendiente) | ❌ | ❌ | ✅ | ✅ |
| Administración de usuarios (`/Usuarios`) | ❌ (va al login) | ❌ | ❌ | ✅ |

### Menú según el rol

| Rol | Opciones del menú |
|---|---|
| Visitante | Inicio, Contáctanos, Acerca de nosotros, Iniciar sesión, Registrarse |
| Cliente | + Productos, Categorías, menú de cuenta (nombre, rol, Cerrar sesión) |
| Vendedor | + Nuevo producto |
| Administrador | + Usuarios |

### Pantallas de cuenta

- **`/Cuenta/Login`** — Inicio de sesión con usuario **o** correo. Tras 5 intentos fallidos la cuenta se bloquea 15 minutos. Después de entrar, regresa a la página que el usuario intentaba abrir.
- **`/Cuenta/Registro`** — Crea una cuenta con rol **Cliente**.
- **`/Cuenta/AccesoDenegado`** — Se muestra cuando el usuario no tiene el rol necesario (indica su rol actual).
- **`/Usuarios`** (solo Administrador) — Buscar usuarios, filtrar por rol, **asignar rol** y **bloquear/desbloquear** cuentas. El Administrador no puede cambiarse el rol ni bloquearse a sí mismo. Los cambios se aplican al usuario afectado en máximo 1 minuto, sin que tenga que cerrar sesión.

### Probar los roles

1. Entra como `admin` / `Admin123!` (o lo que pusiste en tus *user secrets*).
2. Cierra sesión y registra un usuario nuevo: quedará como **Cliente** (solo ve Productos y Categorías, sin botones de gestión).
3. Vuelve a entrar como Administrador, ve a **Usuarios** y cámbialo a **Vendedor**: ya podrá agregar y editar productos, pero no eliminarlos.

> Para las pruebas E2E conviene tener un usuario de cada rol: crea dos cuentas desde **Registrarse** (por ejemplo `vendedor` y `cliente`) y asigna el rol Vendedor a la primera desde **Usuarios**.

---

## 5. Cómo proteger pantallas nuevas

Se usa el atributo `[Authorize]` con los nombres de los roles, como se vio en clase.

**En el controlador:**

```csharp
using Microsoft.AspNetCore.Authorization;

// Reportes: Administrador y Vendedor (según el enunciado)
[Authorize(Roles = "Administrador,Vendedor")]
public class ReportesController : Controller { ... }

// Por acción: los tres roles consultan; la gestión según el rol
[Authorize(Roles = "Administrador,Vendedor,Cliente")]
public class CategoriesController : Controller
{
    public IActionResult Index() { ... }                 // los tres roles

    [Authorize(Roles = "Administrador,Vendedor")]
    public IActionResult Agregar() { ... }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Vendedor")]
    public IActionResult Agregar(Categoria categoria) { ... }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]                 // eliminar: solo Administrador
    public IActionResult Eliminar(int id) { ... }
}
```

> Los nombres de rol deben escribirse **exactamente** así: `Administrador`, `Vendedor`, `Cliente`.
>
> Protege **siempre las dos acciones** (GET y POST). Ocultar el botón en la vista no basta: alguien podría escribir la URL directamente.

**En la vista (mostrar u ocultar botones):**

```cshtml
@if (User.IsInRole("Administrador") || User.IsInRole("Vendedor"))
{
    <a asp-action="Agregar" class="btn btn-primary">Agregar</a>
}

@if (User.Identity?.IsAuthenticated == true)
{
    <span>Hola, @User.Identity.Name</span>
}
```

**Mensajes emergentes (SweetAlert):** desde cualquier controlador puedes usar

```csharp
TempData["SuccessMessage"] = "Categoría agregada correctamente.";   // aviso verde
TempData["ErrorMessage"]   = "No se pudo completar la acción.";     // aviso rojo
```

**Confirmación antes de enviar un formulario:** agrega la clase `swal-confirm-form` y los textos con `data-swal-*`:

```cshtml
<form asp-action="Eliminar" asp-route-id="@item.ID" method="post" class="swal-confirm-form"
      data-swal-title="¿Eliminar esta categoría?"
      data-swal-text="Esta acción no se puede deshacer."
      data-swal-confirm="Sí, eliminar">
    <button type="submit" class="btn btn-outline-danger">Eliminar</button>
</form>
```

---

## 6. Migraciones: reglas para trabajar en equipo

**Después de cada `git pull`**, si llegaron migraciones nuevas (carpeta `TechStore/Migrations`), ejecuta:

```bash
dotnet ef database update
```

**Si cambias un modelo** (agregas una propiedad, una tabla nueva, etc.):

```bash
dotnet ef migrations add NombreDescriptivoDelCambio
dotnet ef database update
```

y sube a Git los **tres** archivos que se generan/modifican: `<fecha>_NombreDelCambio.cs`, `<fecha>_NombreDelCambio.Designer.cs` y `TechStoreContextModelSnapshot.cs`.

Reglas:

- **Avisa al grupo antes de crear una migración** y créala sobre el `master` más reciente. Dos migraciones creadas en paralelo generan conflictos en `TechStoreContextModelSnapshot.cs`.
- **Nunca edites ni borres una migración que ya está en `master`.** Si algo quedó mal, crea otra migración que lo corrija.
- `dotnet ef migrations remove` solo para deshacer una migración **tuya que todavía no subiste**.
- Si hay conflicto en el *snapshot* al hacer merge: quédate con la versión de `master`, borra tu migración, y vuelve a crearla con `dotnet ef migrations add`.

Migraciones actuales:

| Migración | Qué hace |
|---|---|
| `AddEntityFrameworkAndModels` | Tablas de Productos y Categorías |
| `AgregarDescripcionCategoriaYDatosIniciales` | Descripción de categoría y datos iniciales |
| `AgregarIdentity` | Tablas de usuarios y roles de Identity |

---

## 7. Estructura del proyecto

```
TechStore/
├── create_user.sql                 # Crea el login techstore_user en SQL Server
├── TechStore.slnx
├── TechStore.Tests/                # Pruebas unitarias (xUnit + Moq + InMemory)
├── TechStore.IntegrationTests/     # Pruebas de integración (WebApplicationFactory)
├── TechStore.E2E/                  # Pruebas E2E (Playwright)
└── TechStore/
    ├── Program.cs                  # Servicios, Identity, creación de roles y Administrador inicial
    ├── Controllers/
    │   ├── CuentaController.cs     # Login, Registro, Logout, Acceso denegado
    │   ├── UsuariosController.cs   # Administración de usuarios (solo Administrador)
    │   ├── ProductsController.cs   # Catálogo y CRUD protegidos por rol
    │   └── ...
    ├── Data/TechStoreContext.cs    # DbContext (hereda de IdentityDbContext)
    ├── Migrations/
    ├── Models/                     # Entidades y ViewModels (LoginViewModel, RegistroViewModel, ...)
    ├── Services/                   # Capa de negocio + ErroresIdentityEspanol (errores en español)
    ├── Views/
    │   ├── Cuenta/                 # Login, Registro, AccesoDenegado
    │   ├── Usuarios/               # Panel de administración de usuarios
    │   └── Shared/
    │       ├── _Layout.cshtml      # Layout general (navbar con menú de usuario)
    │       └── _AuthLayout.cshtml  # Layout de pantalla completa para Login y Registro
    └── wwwroot/css/
        ├── site.css                # Estilos generales
        └── auth.css                # Estilos de Login y Registro
```

---

## 8. Flujo de trabajo con Git

1. Actualiza `master`: `git checkout master && git pull`.
2. Crea una rama para tu tarea: `git checkout -b feat/nombre-de-la-tarea`.
3. Haz tus cambios, commit y `git push -u origin feat/nombre-de-la-tarea`.
4. Abre un Pull Request hacia `master`.
5. Después de que se acepte un PR, todos hacen `git pull` en `master` (y `dotnet ef database update` si trae migraciones).

---

## 9. Pruebas automatizadas

Hay tres proyectos de pruebas independientes. **No necesitan SQL Server, user secrets ni migraciones**: usan EF Core InMemory y crean su propio usuario Administrador de prueba.

| Proyecto | Qué prueba | Necesita |
|---|---|---|
| `TechStore.Tests` | Unitarias: controladores, servicios y validación de modelos | .NET 10 SDK |
| `TechStore.IntegrationTests` | Integración con `WebApplicationFactory`: roles, `[Authorize]`, antiforgery, login/registro y CRUD sobre HTTP | .NET 10 SDK |
| `TechStore.E2E` | Navegador real (Playwright + Chromium) sobre la app levantada con Kestrel | .NET 10 SDK, **internet** y Chromium instalado (ver más abajo) |

> **Dónde ejecutar:** los comandos de esta sección se ejecutan desde la **raíz de la solución** (la carpeta que contiene `TechStore.slnx`), no desde `TechStore/TechStore`.

### Unitarias e integración (primera vez)

No hay nada que preparar. La primera ejecución restaura los paquetes NuGet automáticamente:

```bash
dotnet test TechStore.Tests
dotnet test TechStore.IntegrationTests
```

Para ejecutar **todo menos los E2E** (tarda unos segundos):

```bash
dotnet test TechStore.slnx --filter "Category!=E2E"
```

### E2E (primera vez)

Los E2E controlan un navegador real, así que antes de la primera ejecución hay que instalar Chromium. Es un paso **único por máquina** (descarga ~150 MB):

```powershell
dotnet build TechStore.E2E
powershell -ExecutionPolicy Bypass -File TechStore.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
```

En macOS/Linux (con PowerShell instalado) usa `pwsh TechStore.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`; en Linux agrega `--with-deps`.

Después ya puedes ejecutarlos:

```bash
dotnet test TechStore.E2E
```

- Tardan alrededor de 30 segundos y corren en modo *headless* (no abren ventana).
- **Necesitan internet** porque el layout carga Bootstrap, jQuery y SweetAlert desde CDN.
- No tocan la base de datos: la app de prueba corre en un puerto libre con BD en memoria.
- Para ver el navegador mientras corren, cambia `Headless = true` por `false` en `TechStore.E2E/E2EFixture.cs`.

### Todo junto, filtros y cobertura

| Qué quiero | Comando |
|---|---|
| Ejecutar absolutamente todo | `dotnet test TechStore.slnx` |
| Solo los E2E | `dotnet test TechStore.slnx --filter "Category=E2E"` |
| Un test o clase concreta | `dotnet test TechStore.Tests --filter "FullyQualifiedName~ProductsControllerTests"` |
| Ver el detalle de cada test | `dotnet test TechStore.Tests --logger "console;verbosity=normal"` |
| Cobertura (unitarias) | `dotnet test TechStore.Tests --collect:"XPlat Code Coverage"` |

### Qué hacer si algo falla

| Síntoma | Solución |
|---|---|
| E2E: `Executable doesn't exist ... chromium` | Falta instalar el navegador; ejecuta el comando `playwright.ps1 install chromium` de arriba (después de `dotnet build TechStore.E2E`). |
| E2E: `playwright.ps1` no existe | Aún no compilaste el proyecto: `dotnet build TechStore.E2E`. |
| E2E: *timeout* esperando un elemento | Revisa tu conexión a internet (CDN) y que no haya otro proceso usando mucha CPU; vuelve a ejecutar. |
| `dotnet test` dice que no encuentra el proyecto | Estás en la carpeta equivocada; vuelve a la raíz donde está `TechStore.slnx`. |
| Error de archivos bloqueados al compilar | Cierra la app si la tienes corriendo (`dotnet run` / Visual Studio) y vuelve a intentar. |

---

## 10. Problemas comunes

| Problema | Solución |
|---|---|
| `A network-related or instance-specific error...` al arrancar | SQL Server no está encendido (`docker start techstore-sql`) o la cadena de conexión no apunta a tu servidor (revisa el paso 3). |
| `Login failed for user 'techstore_user'` | No ejecutaste `create_user.sql` (paso 2). |
| `Invalid object name 'AspNetUsers'` | Falta aplicar la migración: `dotnet ef database update`. |
| No puedo entrar como Administrador | Revisa `dotnet user-secrets list`; si falta `AdminInicial`, configúralo y reinicia la app. Mira la consola: si la contraseña no cumple las reglas, ahí aparece el error. |
| Cambié el rol a alguien y sigue igual | Espera hasta 1 minuto o que la persona recargue la página. |
| La cuenta quedó bloqueada | Por intentos fallidos se desbloquea sola a los 15 minutos; si la bloqueó un Administrador, debe desbloquearla desde **Usuarios**. |
| `docker exec`/`docker cp` dice `C:/Program Files/Git/...: No such file` | Estás en Git Bash, que convierte las rutas que empiezan con `/`. Ejecuta el comando en PowerShell o antepone `MSYS_NO_PATHCONV=1` al comando. |
| `dotnet ef` no se reconoce | `dotnet tool install --global dotnet-ef` y abre una terminal nueva. |
