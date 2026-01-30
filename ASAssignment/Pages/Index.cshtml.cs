using ASAssignment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace ASAssignment.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDataProtectionProvider _provider;

        public ApplicationUser? CurrentUser { get; set; }
        public string? DecryptedCardMasked { get; set; }
        private readonly IDataProtector _protector;

        public IndexModel(ILogger<IndexModel> logger,UserManager<ApplicationUser> userManager, IDataProtectionProvider provider)
        {
            _logger = logger;
            _userManager = userManager;
            _protector = provider.CreateProtector("MySecretKey");
        }

        public async Task OnGetAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) return;
            if (string.IsNullOrWhiteSpace(CurrentUser.CreditCardNoEncrypted))
                return;
            try
            {
                var decrypted = _protector.Unprotect(CurrentUser.CreditCardNoEncrypted);

                // Don’t display full card: mask it but prove decryption happened
                DecryptedCardMasked = decrypted.Length >= 4
                    ? new string('*', decrypted.Length - 4) + decrypted[^4..]
                    : decrypted;
            }
            catch (CryptographicException)
            {
                DecryptedCardMasked = "Unable to decrypt data.";
            }
            catch (Exception)
            {
                DecryptedCardMasked = "Unexpected error occurred.";
            }

        }






    }
}
