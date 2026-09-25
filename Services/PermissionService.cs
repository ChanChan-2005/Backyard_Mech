using Backyard_Mech.Data;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Services
{
    public sealed class PermissionService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Dictionary<string, HashSet<string>> _permissionCache = new(StringComparer.OrdinalIgnoreCase);

        public PermissionService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public static string BuildPermissionCode(string module, string action)
        {
            var moduleKey = new string(module.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return $"{moduleKey}.{action.Trim().ToLowerInvariant()}";
        }

        public async Task<HashSet<string>> GetPermissionCodesAsync(string? roleKey)
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
