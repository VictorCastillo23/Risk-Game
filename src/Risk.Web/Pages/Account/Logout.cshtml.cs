using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Risk.Web.Data;

namespace Risk.Web.Pages.Account;

/// <summary>
/// Spec `user-accounts` — "Logout ends the session". POST-only by
/// construction: there is no <c>OnGet</c> handler that signs anyone out, so
/// a link (a plain GET) can never trigger logout — only an actual form
/// submission (which carries the antiforgery token) can.
/// </summary>
public sealed class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("~/");
    }
}
