using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// === Configuración global de codificaciones y licencia PDF ===
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// === HTTP client para tu API ===
builder.Services.AddHttpClient("Api", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Falta Api:BaseUrl");
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddControllersWithViews();

// ====== Auth: Cookies ======
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/admin/santa/back/Login/Index";
        opt.AccessDeniedPath = "/admin/santa/back/Login/AccessDenied";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.Name = "SR.Auth";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });

// ====== Authorization ======
builder.Services.AddAuthorization(options =>
{
    // --- Policy: Usuario activo ---
    options.AddPolicy("Activo", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim(c =>
                (c.Type == "activo" && (c.Value == "true" || c.Value == "1")) ||
                (c.Type == "estado" && c.Value.Equals("activo", StringComparison.OrdinalIgnoreCase))
            )
        )
    );

    // === Roles CANÓNICOS en los claims ===
    //  - Administrador
    //  - Colaborador
    //  - SoloLectura

    // --- Policies por rol ---
    options.AddPolicy("Admin", p => p.RequireRole("Administrador"));
    options.AddPolicy("Colaborador", p => p.RequireRole("Colaborador"));
    options.AddPolicy("SoloLectura", p => p.RequireRole("SoloLectura"));

    // --- Policy compuesta para crear/modificar (Admin OR Colaborador) ---
    options.AddPolicy("AdminOrColab", p => p.RequireRole("Administrador", "Colaborador"));
});



var app = builder.Build();

// ====== Pipeline ======
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/HomePublic/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseStatusCodePagesWithReExecute("/Login/NotFound");

app.UseAuthentication();   // ?? Debe ir antes de UseAuthorization
app.UseAuthorization();

// ====== Rutas ======
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "HomePublic", action = "IndexPublic" });

app.MapControllerRoute(
    name: "public",
    pattern: "{controller=HomePublic}/{action=IndexPublic}/{id?}");

app.MapControllerRoute(
    name: "backoffice",
    pattern: "admin/santa/back/{controller=Home}/{action=Index}/{id?}");

// ====== Redirección raíz -> sitio público ======
app.MapGet("/", context =>
{
    context.Response.Redirect("/HomePublic/IndexPublic");
    return Task.CompletedTask;
});

app.Run();
