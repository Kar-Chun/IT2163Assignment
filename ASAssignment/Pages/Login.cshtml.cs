using ASAssignment.Model;
using ASAssignment.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<LoginModel> _logger;
        private readonly IWebHostEnvironment _env;

        public LoginModel(SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
                  AuthDbContext db,
                  IHttpClientFactory httpClientFactory,
                  IConfiguration config,
                  ILogger<LoginModel> logger,
                  IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
            _env = env;
        }
        public void OnGet()
        {
            RecaptchaSiteKey = _config["RecaptchaV3:SiteKey"] ?? "";
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            _logger.LogInformation("LOGIN OnPost hit. Trace={Trace}", HttpContext.TraceIdentifier);


            RecaptchaSiteKey = _config["RecaptchaV3:SiteKey"] ?? "";

            //  Validate captcha 
            var token = Request.Form["RecaptchaToken"].ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                ModelState.AddModelError("", "Captcha token missing.");
                return Page();
            }

            // Call ValidateRecaptchaAsync(token) exactly once.
            var (ok, reason) = await ValidateRecaptchaV3Async(token, "login");
            if (!ok)
            {
                ModelState.AddModelError("", reason);
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
            [JsonPropertyName("hostname")] public string? Hostname { get; set; }
            [JsonPropertyName("challenge_ts")] public string? ChallengeTs { get; set; }
            [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
        }

        private async Task<(bool ok, string reason)> ValidateRecaptchaV3Async(string token, string expectedAction)
        {
            // TEMPORARY BYPASS FOR DEVELOPMENT
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("?? RECAPTCHA BYPASSED - Development Mode");
                return (true, "OK (dev bypass)");
            }

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
                var token1 = Request.Form["RecaptchaToken"].ToString(); // or whatever you use
                _logger.LogWarning("Recaptcha token length: {Len}", token1?.Length ?? 0);

                var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify",
                    new FormUrlEncodedContent(form));

                if (!resp.IsSuccessStatusCode) return (false, "Captcha verification failed (HTTP).");

                var json = await resp.Content.ReadAsStringAsync();

                _logger.LogWarning("reCAPTCHA raw: {Json}", json);

                var data = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(json);

                if (data is null)
                    return (false, "Captcha verification failed (no response).");

                if (!data.Success)
                {
                    var codes = data.ErrorCodes == null ? "" : string.Join(", ", data.ErrorCodes);
                    _logger.LogWarning("reCAPTCHA fail. success={Success} host={Host} action={Action} codes={Codes}",
    data?.Success, data?.Hostname, data?.Action,
    data?.ErrorCodes == null ? "" : string.Join(",", data.ErrorCodes));

                    return (false, $"Captcha verification failed ({codes})");

                }


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
