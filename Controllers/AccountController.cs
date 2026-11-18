using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;

namespace Backyard_Mech.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly PermissionService _permissionService;
        private readonly AccountSecurityService _security;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ApplicationDbContext dbContext, PermissionService permissionService, AccountSecurityService security, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _permissionService = permissionService;
            _security = security;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            var current = await _security.ValidateSession(HttpContext);
            if (current is not null)
            {
                return await RedirectToFirstAllowedModuleAsync(current.Role);
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = model.Email.Trim();
            HttpContext.Session.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await LoginLock();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(user =>
                    (user.Email == login || user.UserName == login));
            var state = user is null ? null : await _security.State(user.Id);
            var locked = state?.LockedUntilUtc > DateTime.UtcNow;
            var validPassword = user is not null && PasswordHasher.Verify(model.Password, user.PasswordHash);
            if (user is null) PasswordHasher.Verify(model.Password, DummyHash);
            if (user is null || !user.IsActive || locked || !validPassword || !await _dbContext.AppRoles.AnyAsync(x => x.RoleKey == user.Role && x.IsActive))
            {
                if (state is not null && !locked) _security.Failed(state);
                _security.Audit(HttpContext, "Login failed", user);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                ModelState.AddModelError(string.Empty, "Sign-in failed. Check your credentials or try again later.");
                return View(model);
            }
            if (user.PasswordHash.Contains('.')) user.PasswordHash = PasswordHasher.Hash(model.Password);
            state!.FailedAttempts = 0;
            state.LockedUntilUtc = null;
            _security.Audit(HttpContext, "Login success", user);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            CompleteLogin(user);
            return await RedirectToFirstAllowedModuleAsync(user.Role);
        }

        private static readonly string DummyHash = PasswordHasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        private Task LoginLock() => _dbContext.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'BackyardMech.Login', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000,'Sign-in busy',1;");

        private void CompleteLogin(AppUser user)
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("DevelopmentAuthenticationSignedOut");
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("CredentialStamp", AccountSecurityService.CredentialStamp(user));
            HttpContext.Session.SetString("AuthenticatedAt", DateTime.UtcNow.Ticks.ToString());
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() &&
                HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("DevelopmentAuthentication:Bypass"))
            {
                Response.Cookies.Append("DevelopmentAuthenticationSignedOut", "true", new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied(string module = "this page")
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            ViewData["Module"] = module;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Home()
        {
            var user = await _security.ValidateSession(HttpContext);
            if (user is null)
            {
                return RedirectToAction(nameof(Login));
            }

            return await RedirectToFirstAllowedModuleAsync(user.Role);
        }

        private async Task<IActionResult> RedirectToFirstAllowedModuleAsync(string role)
        {
            var destinations = new[]
            {
                (Module: "Dashboard", Action: "Dashboard"),
                (Module: "Service Queue", Action: "ServiceQueue"),
                (Module: "Customers", Action: "CustomerVehicle"),
                (Module: "Vehicles", Action: "VehicleHistory"),
                (Module: "Appointments", Action: "Appointments"),
                (Module: "Estimates", Action: "EstimateApprovals"),
                (Module: "Work Orders", Action: "WorkOrders"),
                (Module: "Bays", Action: "BayManagement"),
                (Module: "Inventory", Action: "PartsManagement"),
                (Module: "Suppliers", Action: "Suppliers"),
                (Module: "Services", Action: "ServiceCatalog"),
                (Module: "Labour", Action: "LabourTimeTracking"),
                (Module: "Inspections", Action: "QualityControl"),
                (Module: "Tools", Action: "ToolTracking"),
                (Module: "Invoices", Action: "InvoiceRegistry"),
                (Module: "Records", Action: "ArchiveRecords"),
                (Module: "Reports", Action: "Reports"),
                (Module: "Employees", Action: "Employees"),
                (Module: "User Access", Action: "StaffAccounts"),
                (Module: "Settings", Action: "Settings")
            };

            var codes = await _permissionService.GetPermissionCodesAsync(role);
            foreach (var destination in destinations)
            {
                if (OwnerPermissionCatalog.CanAccessAction(codes, destination.Action))
                {
                    return RedirectToAction(destination.Action, "Owner");
                }
            }

            return RedirectToAction(nameof(AccessDenied), new { module = "any application module" });
        }
    }
}
