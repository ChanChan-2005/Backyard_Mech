using Backyard_Mech.Data;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Services
{
    public sealed class PermissionService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _http;
        private readonly Dictionary<string, HashSet<string>> _permissionCache = new(StringComparer.OrdinalIgnoreCase);

        public PermissionService(ApplicationDbContext dbContext, IHttpContextAccessor http)
        {
            _dbContext = dbContext;
            _http = http;
        }

        public static string BuildPermissionCode(string module, string action)
        {
            var moduleKey = new string(module.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return $"{moduleKey}.{action.Trim().ToLowerInvariant()}";
        }

        public static bool CanDelegateStaffPermissions(ISet<string> actor, IEnumerable<string> requested) =>
            actor.Contains("*") || (!requested.Contains("*") &&
                ((actor.Contains("useraccess.view") && actor.Contains("useraccess.edit") && actor.Contains("owneraction.managestaffrolepermissions"))
                || requested.All(actor.Contains)));

        public async Task<HashSet<string>> GetPermissionCodesAsync(string? roleKey, int? userId = null)
        {
            var context = _http.HttpContext;
            if (!userId.HasValue && context is not null && context.Session.GetString("UserRole") == roleKey &&
                int.TryParse(context.Session.GetString("UserId"), out var current)) userId = current;
            var result = new HashSet<string>(await GetRolePermissionCodesAsync(roleKey), StringComparer.OrdinalIgnoreCase);
            if (!userId.HasValue) return result;
            var user = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==userId);
            if(user is null || !user.IsActive || user.Role!=roleKey ||
                !await _dbContext.AppRoles.AnyAsync(x=>x.RoleKey==roleKey && x.IsActive)) return new(StringComparer.OrdinalIgnoreCase);
            // Account access is determined solely by the assigned role.
            return result;
        }

        public async Task<HashSet<string>> GetRolePermissionCodesAsync(string? roleKey)
        {
            roleKey = roleKey?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(roleKey))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (_permissionCache.TryGetValue(roleKey, out var cached))
            {
                return cached;
            }

            var role = await _dbContext.AppRoles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.RoleKey == roleKey && item.IsActive);
            if (role is null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            HashSet<string> permissions;
            if (role.IsSystemProtected)
            {
                permissions = new HashSet<string>(new[] { "*" }, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                permissions = (await (
                    from rolePermission in _dbContext.RolePermissions.AsNoTracking()
                    join permission in _dbContext.Permissions.AsNoTracking()
                        on rolePermission.PermissionId equals permission.PermissionId
                    where rolePermission.RoleId == roleKey && permission.IsActive && permission.PermissionCode != null
                    select permission.PermissionCode!)
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                // A specific action supplies its one-to-one module operation automatically.
                // The editor only shows the action switch for these pairs.
                foreach (var action in OwnerPermissionCatalog.DetailedActions.Where(item =>
                    OwnerPermissionCatalog.HasSingleActionBase(item.Module, item.Action) &&
                    permissions.Contains(BuildPermissionCode(item.Module, "View")) &&
                    permissions.Contains(OwnerPermissionCatalog.ActionPermissionCode(item.Name))))
                    permissions.Add(BuildPermissionCode(action.Module, action.Action));
            }

            _permissionCache[roleKey] = permissions;
            return permissions;
        }

        public async Task<bool> HasPermissionAsync(string? roleKey, string module, string action)
        {
            var permissions = await GetPermissionCodesAsync(roleKey);
            return permissions.Contains("*") || permissions.Contains(BuildPermissionCode(module, action));
        }

        public async Task<string> GetRoleNameAsync(string? roleKey)
        {
            if (string.IsNullOrWhiteSpace(roleKey))
            {
                return "User";
            }

            return await _dbContext.AppRoles.AsNoTracking()
                .Where(role => role.RoleKey == roleKey && role.IsActive)
                .Select(role => role.RoleName)
                .FirstOrDefaultAsync() ?? roleKey;
        }

        public void ClearCache(string? roleKey = null)
        {
            if (string.IsNullOrWhiteSpace(roleKey))
            {
                _permissionCache.Clear();
                return;
            }

            _permissionCache.Remove(roleKey);
        }
    }
}
