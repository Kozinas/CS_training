using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Course.Web.Pages;

[Authorize]
public sealed class IndexModel(LedgerService ledger) : PageModel
{
    public ExpenseView[] Rows { get; private set; } = [];
    [BindProperty] public Guid Id { get; set; }
    [BindProperty, Range(0, long.MaxValue)] public long ExpectedVersion { get; set; }
    [BindProperty, Range(typeof(decimal), "0.01", "999999.99")] public decimal Amount { get; set; }
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task OnGetAsync() => Rows = (await ledger.Page(Owner, 0, 100, HttpContext.RequestAborted)).Rows;
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                await ledger.Update(Owner, Id, new(ExpectedVersion, Amount), HttpContext.RequestAborted);
                return RedirectToPage();
            }
            catch (ContractException ex) { ModelState.AddModelError("", ex.Message); }
        }
        await OnGetAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToPage("/Login");
    }
}
