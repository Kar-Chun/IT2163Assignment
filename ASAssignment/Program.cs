using ASAssignment.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Add this using
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims; // Add this using

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AuthDbContext>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AuthDbContext>();

// Services
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(60); // for demo (change later)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddDataProtection();
// Remove AddDefaultIdentity, as AddIdentity is already used above
// If you want to configure Identity options, do it in AddIdentity:
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = true;   // assignment requirement
                                              // optional password rules here if your module expects them

    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 12;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;

    // Optional but good security
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 3 ;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
});


builder.Services.ConfigureApplicationCookie(Config =>
{
    Config.AccessDeniedPath = "/errors/403";
    Config.LoginPath = "/Login";
});


builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Custom HTTP status pages (404, 403, etc.)
app.UseStatusCodePagesWithRedirects("/errors/{0}");

app.UseRouting();

app.UseSession();
app.UseAuthentication();

app.UseExceptionHandler("/errors/500");

app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    // Allow public pages
    var isIdentity = path.StartsWithSegments("/Identity");
    var isCustomRegister = path.StartsWithSegments("/Register");
    var isPublic = isIdentity || isCustomRegister;
    // Only protect authenticated requests 
    if (context.User?.Identity?.IsAuthenticated == true && !isPublic)
    {
        var sToken = context.Session.GetString("AuthToken");
        var cToken = context.Request.Cookies["AuthToken"];

        // session expired or missing -> redirect to login
        if (string.IsNullOrEmpty(sToken) || string.IsNullOrEmpty(cToken) || sToken != cToken)
        {
            await context.SignOutAsync(); // clear identity cookie

            //  clear session
            context.Session.Clear();
            //  delete cookies
            context.Response.Cookies.Delete("AuthToken");
            context.Response.Cookies.Delete(".AspNetCore.Session");
            context.Response.Cookies.Delete(".AspNetCore.Identity.Application");

            context.Response.Redirect("/Login");
            return;
        }


        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userMgr = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userMgr.FindByIdAsync(userId);

        if (user == null || user.ActiveAuthToken != cToken)
        {
            await context.SignOutAsync();
            context.Session.Clear();
            context.Response.Redirect("/Login?reason=multi");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();
