using Backyard_Mech.Data;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        var ownerFeatureFolders = new[]
        {
            "Dashboard",
            "Customers",
            "Service",
            "Appointments",
            "Estimates",
            "WorkOrders",
            "Bay",
            "Parts",
            "Labour",
            "QualityControl",
            "Tools",
            "Invoicing",
            "Records",
            "Employees",
            "Reports",
            "Settings"
        };

        foreach (var folder in ownerFeatureFolders)
        {
            options.ViewLocationFormats.Add($"/Views/Owner/{folder}/{{0}}.cshtml");
        }
    });
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddScoped<RecordsDispositionService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<OwnerPermissionFilter>();
builder.Services.AddSingleton<ReportExportService>();
builder.Services.AddHostedService<RecordsDispositionCleanupService>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseSeeder.SeedAsync(dbContext, app.Configuration);
}

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
