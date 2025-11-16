using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SantaRamona.Backoffice.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly IConfiguration _config;

        // Ajustá si el ID de estado ACTIVO no es 1
        private const int ACTIVE_STATE_ID = 1;

        public LoginController(IConfiguration config) => _config = config;

        // ===================== GET: /Login =====================
        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity is { IsAuthenticated: true })
                return Redirect("/admin/santa/back");
            return View();
        }

        // ===================== POST: /Login/Login =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Completá usuario y contraseña.";
                return View("Index");
            }

            var cs = _config.GetConnectionString("DefaultConnection")!;
            using var cnn = new SqlConnection(cs);
            await cnn.OpenAsync();

            // === Usuario base ===
            var cmdUser = new SqlCommand(@"
                SELECT TOP 1
                    u.id_usuario,
                    u.email,
                    u.nombre,
                    u.apellido,
                    u.clave,
                    u.id_estadoUsuario
                FROM Usuario u
                WHERE u.email = @Email;", cnn);
            cmdUser.Parameters.AddWithValue("@Email", username);

            using var rd = await cmdUser.ExecuteReaderAsync();
            if (!rd.Read())
            {
                ViewBag.Error = "Usuario o contraseña inválidos.";
                return View("Index");
            }

            int idUsuario = rd.GetInt32(rd.GetOrdinal("id_usuario"));
            string email = rd["email"]?.ToString() ?? "";
            string nombre = rd["nombre"]?.ToString() ?? "";
            string apellido = rd["apellido"]?.ToString() ?? "";
            string claveDb = rd["clave"]?.ToString() ?? "";
            int idEstadoUsuario = rd.IsDBNull(rd.GetOrdinal("id_estadoUsuario")) ? 0
                                  : rd.GetInt32(rd.GetOrdinal("id_estadoUsuario"));
            rd.Close();

            // === Validar contraseña (plaintext según tu implementación actual) ===
            if (!string.Equals(password, claveDb))
            {
                ViewBag.Error = "Usuario o contraseña inválidos.";
                return View("Index");
            }

            // === Claim de estado para policy "Activo" ===
            string estadoClaim = (idEstadoUsuario == ACTIVE_STATE_ID) ? "activo" : "inactivo";

            // === Traer rol del usuario (1-N por columna id_rol en USUARIO) ===
            var roles = new List<string>();

            var cmdRol = new SqlCommand(@"
                SELECT r.descripcion AS rolNombre
                FROM Usuario u
                INNER JOIN Rol r ON u.id_rol = r.id_rol
                WHERE u.id_usuario = @IdUsuario;", cnn);

            cmdRol.Parameters.AddWithValue("@IdUsuario", idUsuario);

            using (var rr = await cmdRol.ExecuteReaderAsync())
            {
                while (await rr.ReadAsync())
                {
                    var rolNombre = (rr["rolNombre"]?.ToString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(rolNombre))
                        roles.Add(rolNombre);
                }
            }

            // === Normalización de roles a nombres esperados por policies ===
            //  - Administrador  => "Administrador"
            //  - Colaborador    => "Colaborador"
            //  - Solo lectura   => "SoloLectura"
            static string NormalizeRole(string raw)
            {
                var t = (raw ?? "").Trim().ToLowerInvariant();
                return t switch
                {
                    "administrador" => "Administrador",
                    "admin" => "Administrador",

                    "colaborador" => "Colaborador",

                    "sololectura" => "SoloLectura",
                    "solo lectura" => "SoloLectura",
                    "lectura" => "SoloLectura",
                    "readonly" => "SoloLectura",

                    _ => raw?.Trim() ?? "" // fallback: dejar tal cual
                };
            }

            var rolesNorm = roles
                .Select(NormalizeRole)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // === Claims ===
            var display = string.IsNullOrWhiteSpace((nombre + " " + apellido).Trim())
                ? email
                : $"{nombre} {apellido}".Trim();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
                new Claim(ClaimTypes.Name, display),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.GivenName, nombre ?? ""),
                new Claim(ClaimTypes.Surname, apellido ?? ""),
                new Claim("estado", estadoClaim) // para policy "Activo"
            };

            foreach (var rname in rolesNorm)
                claims.Add(new Claim(ClaimTypes.Role, rname));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });

            return Redirect("/admin/santa/back");
        }

        // ===================== LOGOUT =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Login");
        }

        // ===================== ACCESS DENIED =====================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewBag.Illustration = Url.Content("~/images/santaramona.png");
            return View();
        }

        // ===================== RECUPERAR CLAVE (solo vista) =====================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Recuperar()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotFound()
        {
            return View();
        }
    }
}
