using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ASAssignment.Model
{
    public class ApplicationUser: IdentityUser
    {
        [Required, StringLength(50)]
        public string FirstName { get; set; } = "";

        [Required, StringLength(50)]
        public string LastName { get; set; } = "";

        [Required, StringLength(20)]
        public string MobileNo { get; set; } = "";

        [Required, StringLength(300)]
        public string BillingAddress { get; set; } = "";

        [Required, StringLength(300)]
        public string ShippingAddress { get; set; } = "";

        [Required, StringLength(800)]
        public string CreditCardNoEncrypted { get; set; } = "";

        // Store file path only (NOT raw image bytes)
        [Required, StringLength(300)]
        public string PhotoPath { get; set; } = "";

        public string? ActiveAuthToken { get; set; }
    }
}
