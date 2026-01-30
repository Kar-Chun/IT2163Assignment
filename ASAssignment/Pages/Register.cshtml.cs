using ASAssignment.Model;
using ASAssignment.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace ASAssignment.Pages
{
    public class RegisterModel : PageModel
    {
        private UserManager<ApplicationUser> userManager { get; }
        private SignInManager<ApplicationUser> signInManager { get; }
        private readonly IWebHostEnvironment _env;
        private readonly IDataProtector _protector;
        [BindProperty]
        public Register RModel { get; set; }
        public RegisterModel(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, IWebHostEnvironment env, IDataProtectionProvider provider)
        
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            _env = env;
            _protector = provider.CreateProtector("MySecretKey"); 
        }
        public void OnGet()
        {
        }
        //Save data into the database


        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var billingEncoded = WebUtility.HtmlEncode(RModel.BillingAddress);
                var shippingEncoded = WebUtility.HtmlEncode(RModel.ShippingAddress);
                var firstEncoded = WebUtility.HtmlEncode(RModel.FirstName);
                var lastEncoded = WebUtility.HtmlEncode(RModel.LastName);

                // Save photo
                try
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                    Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"{Guid.NewGuid():N}.jpg";
                    var fullPath = Path.Combine(uploadsFolder, fileName);

                    using (var fs = new FileStream(fullPath, FileMode.Create))
                        await RModel.Photo.CopyToAsync(fs);

                    var photoPath = $"/uploads/profiles/{fileName}";




                    var user = new ApplicationUser()
                    {
                        UserName = RModel.Email,
                        Email = RModel.Email,

                        FirstName = firstEncoded,
                        LastName = lastEncoded,
                        MobileNo = RModel.MobileNo,
                        BillingAddress = billingEncoded,
                        ShippingAddress = shippingEncoded,

                        CreditCardNoEncrypted = _protector.Protect(RModel.CreditCardNo),
                        PhotoPath = photoPath
                    };
                    var result = await userManager.CreateAsync(user, RModel.Password);
                    if (result.Succeeded)
                    {
                        await signInManager.SignInAsync(user, false);
                        return RedirectToPage("Index");
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("",
                    error.Description);
                    }
                }
                catch (IOException)
                {
                    ModelState.AddModelError(string.Empty, "Photo upload failed. Please try again.");
                    return Page();
                }
                catch (Exception)
                {
                    ModelState.AddModelError(string.Empty, "Unexpected error during photo upload.");
                    return Page();
                }
            }
            return Page();  
        }
    }
}
