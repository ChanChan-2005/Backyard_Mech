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
            // Protected owner is the recovery account; individual overrides apply to ordinary roles.
            if(result.Contains("*")) return result;
            var overrides = await (from o in _dbContext.UserPermissionOverrides
                join p in _dbContext.Permissions on o.PermissionId equals p.PermissionId
                where o.UserId==userId && p.IsActive
                select new {p.PermissionCode,p.ModuleName,p.ActionName,o.IsAllowed}).ToListAsync();
            foreach(var item in overrides.Where(x=>OwnerPermissionCatalog.IsSupportedPermission(x.ModuleName??"",x.ActionName??"")))
                if(item.IsAllowed) result.Add(item.PermissionCode!); else result.Remove(item.PermissionCode!);
            // Denied module visibility must also prevent direct writes to that module.
            foreach(var item in overrides.Where(x=>!x.IsAllowed && x.ActionName=="View"))
            {
                var prefix=BuildPermissionCode(item.ModuleName??"", "");
                result.RemoveWhere(x=>x.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
            }
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
