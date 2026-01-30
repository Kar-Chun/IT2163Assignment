using System.ComponentModel.DataAnnotations;

namespace ASAssignment.ViewModels
{
    public class Register
    {
        [Required, StringLength(50)]
        [DataType(DataType.Text)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "First name can only contain letters, spaces, ' and -.")]
        public string FirstName { get; set; } = "";

        [Required, StringLength(50)]
        [DataType(DataType.Text)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Last name can only contain letters, spaces, ' and -.")]
        public string LastName { get; set; } = "";

        // Input plain, encrypt in Register.cshtml.cs before saving
        [Required]
        [DataType(DataType.CreditCard)]
        [RegularExpression(@"^\d{12,19}$", ErrorMessage = "Credit Card No must be 12–19 digits.")]
        public string CreditCardNo { get; set; } = "";

        [Required]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "Mobile number must be 8 digits.")]
        public string MobileNo { get; set; } = "";

        [Required, StringLength(300)]
        public string BillingAddress { get; set; } = "";

        // allow all special chars -> no regex
        [Required, StringLength(300)]
        public string ShippingAddress { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(12, ErrorMessage = "Enter at least 12 characters password")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[$@$!%*?&]).{12,}$",
    ErrorMessage = "Must have upper, lower, digit, and a special character.")]
        public string Password { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Password and Confirm Password do not match.")]
        public string ConfirmPassword { get; set; } = "";

        // JPG only enforced server-side in code-behind
        [Required]
        public IFormFile Photo { get; set; } = default!;

    }
}
