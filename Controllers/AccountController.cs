using Backyard_Mech.Data;
using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Backyard_Mech.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly PermissionService _permissionService;

        public AccountController(ApplicationDbContext dbContext, PermissionService permissionService)
        {
            _dbContext = dbContext;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (!string.IsNullOrWhiteSpace(role))
            {
                return await RedirectToFirstAllowedModuleAsync(role);
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = model.Email.Trim();
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.IsActive &&
                    (user.Email == login || user.UserName == login));

            if (user is null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("UserId", user.Id.ToString());

            return await RedirectToFirstAllowedModuleAsync(user.Role);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
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
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrWhiteSpace(role))
            {
                return RedirectToAction(nameof(Login));
            }

            return await RedirectToFirstAllowedModuleAsync(role);
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
                (Module: "Labour", Action: "LabourTimeTracking"),
                (Module: "Inspections", Action: "QualityControl"),
                (Module: "Tools", Action: "ToolTracking"),
                (Module: "Invoices", Action: "InvoiceRegistry"),
                (Module: "Records", Action: "ArchiveRecords"),
                (Module: "Reports", Action: "Reports"),
                (Module: "Employees", Action: "Employees"),
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
