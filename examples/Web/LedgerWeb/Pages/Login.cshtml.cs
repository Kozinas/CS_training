using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Course.Web.Pages;

[EnableRateLimiting("auth")]
public sealed class LoginModel(SignInManager<WebUser> signIn) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128)] public string Password { get; set; } = "";
    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Email, Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Не удалось войти. Проверьте данные или попробуйте позже.");
            return Page();
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
