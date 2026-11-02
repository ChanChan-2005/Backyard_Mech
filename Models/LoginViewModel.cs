using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models
{
    public class LoginViewModel
    {
        public bool EnableMfa { get; set; }
        [Required(ErrorMessage = "Email address is required.")]
        [Display(Name = "Email Address")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(1024)]
        public string Password { get; set; } = string.Empty;
    }
}
