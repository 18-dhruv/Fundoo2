using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2,
            ErrorMessage = "First name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$",
            ErrorMessage = "First name can contain only letters, spaces, hyphens, or apostrophes.")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2,
            ErrorMessage = "Last name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$",
            ErrorMessage = "Last name can contain only letters, spaces, hyphens, or apostrophes.")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [RegularExpression(
            @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d\s])\S{8,}$",
            ErrorMessage = "Password must contain at least one letter, one digit, and one special character, with no spaces.")]
        public string Password { get; set; } = "";
    }
}