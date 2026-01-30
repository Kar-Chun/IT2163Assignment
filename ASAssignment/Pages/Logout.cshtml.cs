using ASAssignment.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ASAssignment.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private UserManager<ApplicationUser> _userManager { get; }
        private readonly AuthDbContext _db;
        public LogoutModel(SignInManager<ApplicationUser> signInManager,
                        UserManager<ApplicationUser> userManager,
                        AuthDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
        }
        public void OnGet()
        {
        }
        public async Task<IActionResult> OnPostLogoutAsync()
        {
            var email = User?.Identity?.Name;

            // Write audit log first (while still authenticated)
            if (!string.IsNullOrEmpty(email))
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = _userManager.GetUserId(User),
                    Email = email,
                    Action = "LOGOUT",
                    IsSuccess = true,
                });
                await _db.SaveChangesAsync();
            }

            // invalidate token in DB
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.ActiveAuthToken = null;
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();

            // expire AuthToken cookie
            Response.Cookies.Delete("AuthToken");

            // expire session cookie (ASP.NET Core name)
            Response.Cookies.Delete(".AspNetCore.Session");
            //HttpContext.Session.Clear();
            return RedirectToPage("Login");
        }
        public async Task<IActionResult> OnPostDontLogoutAsync()
        {
            return RedirectToPage("Index");
        }
    }
}
