

using Biblioteka.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BibliotekaContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Ścieżka, na którą użytkownik zostanie przekierowany przy próbie dostępu bez logowania
        options.LoginPath = "/Home/Login";
        options.LogoutPath = "/Home/Index";
        options.AccessDeniedPath = "/Home/AccessDenied"; // Opcjonalnie: strona przy braku uprawnień (rola)
    });

builder.Services.AddDistributedMemoryCache(); // Użycie pamięci podręcznej dla przechowywania sesji
builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30); // Czas trwania sesji
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");





app.Run();
