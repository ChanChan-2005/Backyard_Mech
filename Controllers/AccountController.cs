using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;
using OtpNet;

namespace Backyard_Mech.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly PermissionService _permissionService;
        private readonly AccountSecurityService _security;
        private readonly IDataProtector _protector;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ApplicationDbContext dbContext, PermissionService permissionService, AccountSecurityService security, IDataProtectionProvider protection, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _permissionService = permissionService;
            _security = security;
            _protector = protection.CreateProtector("BackyardMech.Authenticator.v1");
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
            var needMfa = state!.AuthenticatorSecret is not null || await _security.RequiresMfa(user) || model.EnableMfa;
            _security.Audit(HttpContext, needMfa ? "Login MFA pending" : "Login success", user);
            if (!needMfa) { state.FailedAttempts = 0; state.LockedUntilUtc = null; }
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            if (needMfa)
            {
                HttpContext.Session.SetString("PendingUserId", user.Id.ToString());
                HttpContext.Session.SetString("PendingStamp", AccountSecurityService.CredentialStamp(user));
                HttpContext.Session.SetString("PendingRole", user.Role);
                HttpContext.Session.SetString("PendingUntil", DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
                return RedirectToAction(nameof(Mfa));
            }
            CompleteLogin(user, false);
            return await RedirectToFirstAllowedModuleAsync(user.Role);
        }

        private static readonly string DummyHash = PasswordHasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        private Task LoginLock() => _dbContext.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'BackyardMech.Login', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000,'Sign-in busy',1;");

        private void CompleteLogin(AppUser user, bool mfa)
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("DevelopmentAuthenticationSignedOut");
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("CredentialStamp", AccountSecurityService.CredentialStamp(user));
            HttpContext.Session.SetString("AuthenticatedAt", DateTime.UtcNow.Ticks.ToString());
            if (mfa) HttpContext.Session.SetString("MfaVerified", "true");
        }

        private async Task<AppUser?> PendingUser()
        {
            if (!int.TryParse(HttpContext.Session.GetString("PendingUserId"), out var id) ||
                !long.TryParse(HttpContext.Session.GetString("PendingUntil"), out var expires) || expires < DateTime.UtcNow.Ticks) return null;
            var user = await _dbContext.Users.FindAsync(id);
            if (user is null || !user.IsActive || user.Role != HttpContext.Session.GetString("PendingRole") ||
                AccountSecurityService.CredentialStamp(user) != HttpContext.Session.GetString("PendingStamp")) return null;
            return user;
        }

        [HttpGet, EnableRateLimiting("login")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Mfa()
        {
            var user = await PendingUser();
            if (user is null) return RedirectToAction(nameof(Login));
            var state = await _dbContext.AccountSecurity.AsNoTracking().SingleAsync(x => x.UserId == user.Id);
            if (state.LockedUntilUtc > DateTime.UtcNow) { HttpContext.Session.Clear(); return RedirectToAction(nameof(Login)); }
            return View(MfaModel(state));
        }

        private MfaForm MfaModel(AccountSecurity state)
        {
            var model = new MfaForm();
            if (state.AuthenticatorSecret is null)
            {
                var key = HttpContext.Session.GetString("SetupKey") ?? Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
                HttpContext.Session.SetString("SetupKey", key);
                model.SetupKey = key;
            }
            return model;
        }

        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Mfa(MfaForm form)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await LoginLock();
            var user = await PendingUser();
            if (user is null) return RedirectToAction(nameof(Login));
            var state = await _security.State(user.Id);
            if (state.LockedUntilUtc > DateTime.UtcNow) { HttpContext.Session.Clear(); return RedirectToAction(nameof(Login)); }
            var enrolling = state.AuthenticatorSecret is null;
            var code = (form.Code ?? "").Trim().Replace(" ", "");
            var key = enrolling ? HttpContext.Session.GetString("SetupKey") : _protector.Unprotect(state.AuthenticatorSecret!);
            long step = -1;
            var valid = ModelState.IsValid && key is not null && new Totp(Base32Encoding.ToBytes(key)).VerifyTotp(code, out step, new VerificationWindow(1, 1)) && step > state.LastOtpStep;
            var recoveryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
            var recovery = !enrolling && ModelState.IsValid && state.RecoveryHashes is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recoveryHash), Encoding.UTF8.GetBytes(state.RecoveryHashes));
            if (!valid && !recovery)
            {
                _security.Failed(state);
                _security.Audit(HttpContext, "MFA failed", user);
                await _dbContext.SaveChangesAsync(); await transaction.CommitAsync();
                ModelState.AddModelError(string.Empty, "Invalid or already-used code. Try again later if locked.");
                return View(MfaModel(state));
            }
            string? recoveryCode = null;
            if (enrolling || recovery)
            {
                recoveryCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                state.RecoveryHashes = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recoveryCode)));
            }
            if (enrolling) state.AuthenticatorSecret = _protector.Protect(key!);
            if (valid) state.LastOtpStep = step;
            state.FailedAttempts = 0; state.LockedUntilUtc = null;
            _security.Audit(HttpContext, enrolling ? "MFA enabled" : recovery ? "MFA recovery used" : "MFA verified", user);
            _security.Audit(HttpContext, "Login success", user);
            await _dbContext.SaveChangesAsync(); await transaction.CommitAsync();
            CompleteLogin(user, true);
            if (recoveryCode is not null) return View("MfaRecovery", new MfaForm { RecoveryCode = recoveryCode });
            return await RedirectToFirstAllowedModuleAsync(user.Role);
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
                (Module: "Vehicles", Action: "CustomerVehicle"),
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

            foreach (var destination in destinations)
            {
                if (await _permissionService.HasPermissionAsync(role, destination.Module, "View"))
                {
                    return RedirectToAction(destination.Action, "Owner");
                }
            }

            return RedirectToAction(nameof(AccessDenied), new { module = "any application module" });
        }
    }
}
