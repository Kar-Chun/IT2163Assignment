using ASAssignment.Model;
using ASAssignment.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ASAssignment.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public Login LModel { get; set; }

        private readonly SignInManager<ApplicationUser> _signInManager;
        private UserManager<ApplicationUser> _userManager { get; }
        private readonly AuthDbContext _db;

        public string RecaptchaSiteKey { get; private set; } = "";

        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public LoginModel(SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
                  AuthDbContext db,
                  IHttpClientFactory httpClientFactory,
                  IConfiguration config)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }
        public void OnGet()
        {
            RecaptchaSiteKey = _config["RecaptchaV3:SiteKey"] ?? "";
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            RecaptchaSiteKey = _config["RecaptchaV3:SiteKey"] ?? "";

            //  Validate captcha 
            var token = Request.Form["g-recaptcha-response"].ToString();
            var (ok, reason) = await ValidateRecaptchaV3Async(token, "Login");
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, reason);
                return Page();
            }
            if (ModelState.IsValid)
            {
                var identityResult = await _signInManager.PasswordSignInAsync(LModel.Email,
                LModel.Password, LModel.RememberMe, lockoutOnFailure:true);
                if (identityResult.Succeeded)
                {
                    await WriteAuditAsync("LOGIN_SUCCESS", true, LModel.Email);
                    // after successful sign-in
                    HttpContext.Session.SetString("LoggedIn", "1");
                    HttpContext.Session.SetString("Email", LModel.Email);
                    HttpContext.Session.SetString("LastLoginUtc", DateTime.UtcNow.ToString("O"));

                    var guid = Guid.NewGuid().ToString("N");

                    //session fixation prevention
                    // store in session
                    HttpContext.Session.SetString("AuthToken", guid);

                    // store in cookie
                    Response.Cookies.Append("AuthToken", guid, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                    });

                    var user = await _userManager.FindByEmailAsync(LModel.Email);
                    user.ActiveAuthToken = guid;
                    await _userManager.UpdateAsync(user);

                    return RedirectToPage("Index");
                }
                if (identityResult.IsLockedOut)
                {
                    await WriteAuditAsync("LOCKED_OUT", false, LModel.Email);
                    ModelState.AddModelError(string.Empty, "Account locked. Try again later.");
                    return Page();
                }

                // normal fail
                await WriteAuditAsync("LOGIN_FAIL", false, LModel.Email);
                ModelState.AddModelError("", "Username or Password incorrect");

            }

            return Page();
        }
        private async Task WriteAuditAsync(string action, bool success, string? email)
        {
            var user = email == null ? null : await _userManager.FindByEmailAsync(email);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user?.Id,
                Email = email,
                Action = action,
                IsSuccess = success,

            });

            await _db.SaveChangesAsync();
        }

        private class RecaptchaVerifyResponse
        {
            [JsonPropertyName("success")] public bool Success { get; set; }
            [JsonPropertyName("score")] public double Score { get; set; }
            [JsonPropertyName("action")] public string? Action { get; set; }

            [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
        }

        private async Task<(bool ok, string reason)> ValidateRecaptchaV3Async(string token, string expectedAction)
        {
            var secret = _config["RecaptchaV3:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret)) return (false, "Captcha secret key missing.");

            if (string.IsNullOrWhiteSpace(token)) return (false, "Captcha token missing.");

            var client = _httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token
            };
            try
            {
                var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify",
                    new FormUrlEncodedContent(form));

                if (!resp.IsSuccessStatusCode) return (false, "Captcha verification failed (HTTP).");

                var json = await resp.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(json);

                if (data is null || !data.Success) return (false, "Captcha verification failed.");


                var minScoreStr = _config["RecaptchaV3:MinScore"];
                var minScore = double.TryParse(minScoreStr, out var s) ? s : 0.5;

                if (!string.Equals(data.Action, expectedAction, StringComparison.OrdinalIgnoreCase))
                    return (false, "Captcha action mismatch.");

                if (data.Score < minScore)
                    return (false, $"Captcha score too low ({data.Score:0.00}).");

                return (true, "OK");
            }
            catch (HttpRequestException)
            {
                return (false, "Captcha service is unavailable. Please try again.");
            }
            catch (TaskCanceledException)
            {
                return (false, "Captcha verification timed out. Please try again.");
            }
            catch (Exception)
            {
                return (false, "Captcha verification error. Please try again.");
            }
        }
    }
}
