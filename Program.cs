var builder = WebApplication.CreateBuilder(args);

// === Servicios ===
builder.Services.AddHttpClient("Api", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Falta configurar Api:BaseUrl en appsettings*.json");

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddControllersWithViews();

// === Build (desde acá ya NO se agregan servicios) ===
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// que abra Home/Index por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HomePublic}/{action=IndexPublic}/{id?}");


app.Run();

