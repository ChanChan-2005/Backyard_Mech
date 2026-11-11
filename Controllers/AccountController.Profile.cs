using Backyard_Mech.Models;
using Backyard_Mech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class AccountController
{
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return RedirectToAction(nameof(Login));

        var employee = await _dbContext.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == user.Id.ToString());
        var model = new ProfileSettingsViewModel
        {
            UserName = user.UserName,
            LoginEmail = user.Email,
            RoleName = await _permissionService.GetRoleNameAsync(user.Role),
            HasEmployeeProfile = employee is not null,
            HasPhoto = ProfilePhotoStore.Find(_environment.ContentRootPath, user.Id) is not null,
            FirstName = employee?.FirstName ?? string.Empty,
            LastName = employee?.LastName ?? string.Empty,
            Phone = employee?.Phone ?? string.Empty,
            ContactEmail = employee?.Email ?? string.Empty
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateOwnProfileForm form)
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid)
        {
            SetProfileMessage("Check your name, phone, and contact email.", true);
            return RedirectToAction(nameof(Profile));
        }

        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == user.Id.ToString());
        if (employee is null) return StatusCode(StatusCodes.Status403Forbidden);

        employee.FirstName = form.FirstName.Trim();
        employee.LastName = form.LastName.Trim();
        employee.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        employee.Email = string.IsNullOrWhiteSpace(form.ContactEmail) ? null : form.ContactEmail.Trim();
        _security.Audit(HttpContext, "Update own profile", user);
        await _dbContext.SaveChangesAsync();
        SetProfileMessage("Profile details updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordForm form)
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid ||
            !form.NewPassword.Any(char.IsUpper) || !form.NewPassword.Any(char.IsLower) ||
            !form.NewPassword.Any(char.IsDigit) || !form.NewPassword.Any(character => !char.IsLetterOrDigit(character)))
        {
            SetProfileMessage("Use at least 12 characters with uppercase, lowercase, a number, and a symbol. Confirm the new password matches.", true);
            return RedirectToAction(nameof(Profile));
        }

        var account = await _dbContext.Users.FindAsync(user.Id);
        if (account is null) return RedirectToAction(nameof(Login));
        if (!PasswordHasher.Verify(form.CurrentPassword, account.PasswordHash))
        {
            SetProfileMessage("Current password is incorrect.", true);
            return RedirectToAction(nameof(Profile));
        }

        account.PasswordHash = PasswordHasher.Hash(form.NewPassword);
        _security.Audit(HttpContext, "Change own password", account);
        await _dbContext.SaveChangesAsync();
        HttpContext.Session.SetString("CredentialStamp", AccountSecurityService.CredentialStamp(account));
        SetProfileMessage("Password changed successfully.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile? photo)
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return RedirectToAction(nameof(Login));
        if (photo is null || photo.Length is <= 0 or > 2 * 1024 * 1024)
        {
            SetProfileMessage("Choose a JPG, PNG, or WebP image no larger than 2 MB.", true);
            return RedirectToAction(nameof(Profile));
        }

        var extension = photo.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };
        if (extension is null)
        {
            SetProfileMessage("Only JPG, PNG, and WebP images are accepted.", true);
            return RedirectToAction(nameof(Profile));
        }

        var header = new byte[12];
        await using (var stream = photo.OpenReadStream())
        {
            if (photo.Length < header.Length)
            {
                SetProfileMessage("The selected image is invalid.", true);
                return RedirectToAction(nameof(Profile));
            }
            await stream.ReadExactlyAsync(header);
        }
        var valid = extension switch
        {
            ".jpg" => header.AsSpan(0, 3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid)
        {
            SetProfileMessage("The image content does not match its file type.", true);
            return RedirectToAction(nameof(Profile));
        }

        await ProfilePhotoStore.SaveAsync(_environment.ContentRootPath, user.Id, photo, extension);
        _security.Audit(HttpContext, "Update own profile photo", user);
        await _dbContext.SaveChangesAsync();
        SetProfileMessage("Profile picture updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProfilePhoto()
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return RedirectToAction(nameof(Login));
        ProfilePhotoStore.Remove(_environment.ContentRootPath, user.Id);
        _security.Audit(HttpContext, "Remove own profile photo", user);
        await _dbContext.SaveChangesAsync();
        SetProfileMessage("Profile picture removed.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public async Task<IActionResult> ProfilePhoto()
    {
        var user = await _security.ValidateSession(HttpContext);
        if (user is null) return Unauthorized();
        var photo = ProfilePhotoStore.Find(_environment.ContentRootPath, user.Id);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(photo.Value.Path, photo.Value.ContentType);
    }

    private void SetProfileMessage(string message, bool error = false)
    {
        TempData["OwnerMessage"] = message;
        TempData["OwnerMessageTone"] = error ? "error" : "success";
    }
}
