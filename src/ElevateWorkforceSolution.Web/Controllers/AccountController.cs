using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Sign in, sign up and account settings. Registration only ever creates a candidate or an
/// employer - the administrator role is reserved for seeding, so no public route can mint one.
/// </summary>
[Authorize]
public class AccountController : ApplicationControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICandidateProfileService _candidateProfiles;
    private readonly IEmployerProfileService _employerProfiles;

    public AccountController(
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICandidateProfileService candidateProfiles,
        IEmployerProfileService employerProfiles)
        : base(currentUser)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _candidateProfiles = candidateProfiles;
        _employerProfiles = employerProfiles;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsSignedIn)
        {
            return RedirectToDashboard();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact an administrator.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _ = _signInManager.RefreshSignInAsync(user);
            return SafeRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again in 10 minutes.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult Register(AccountType? accountType = null)
    {
        if (IsSignedIn)
        {
            return RedirectToDashboard();
        }

        return View(new RegisterViewModel
        {
            RegisterAsJobSeeker = accountType is null or AccountType.JobSeeker
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Belt and braces: the role assigned below is chosen from the two public account types
        // only, so a crafted form post cannot create an administrator.
        var role = model.AccountType == AccountType.JobSeeker ? Roles.JobSeeker : Roles.Employer;

        if (role == Roles.Employer && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            ModelState.AddModelError(nameof(model.CompanyName), "Employers must provide a company name.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            IsActive = role != Roles.Employer,
            EmailConfirmed = true
        };

        var created = await _userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            // Roll the account back so a half-provisioned user never exists.
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "We could not finish setting up your account. Please try again.");
            return View(model);
        }

        await ProvisionRoleProfileAsync(user.Id, role, model);

        if (role == Roles.Employer)
        {
            FlashSuccess("Your employer account has been created and is pending administrator verification before you can sign in.");
            return RedirectToAction(nameof(Login));
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        FlashSuccess($"Welcome to Elevate Workforce, {user.FirstName}.");
        return RedirectToDashboard();
    }

    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(new AccountProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(AccountProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailChanged = !string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        var emailTaken = emailChanged
            && await _userManager.FindByEmailAsync(model.Email.Trim()) is not null;

        if (emailTaken)
        {
            ModelState.AddModelError(nameof(model.Email), "That email address is already in use.");
            return View(model);
        }

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        if (emailChanged)
        {
            user.Email = model.Email.Trim();
            user.NormalizedEmail = _userManager.NormalizeEmail(model.Email.Trim());
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        FlashSuccess("Your account details have been updated.");
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        FlashSuccess("Your password has been changed.");
        return RedirectToAction(nameof(ChangePassword));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>Creates the role-specific profile row so the rest of the app can rely on it existing.</summary>
    private async Task ProvisionRoleProfileAsync(string userId, string role, RegisterViewModel model)
    {
        if (role == Roles.JobSeeker)
        {
            var result = await _candidateProfiles.EnsureProfileAsync(userId, null, model.PhoneNumber);
            if (!result.Succeeded)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>();
                logger.LogWarning("Could not create candidate profile for {UserId}: {Error}", userId, result.Error);
            }
        }
        else if (role == Roles.Employer)
        {
            var result = await _employerProfiles.EnsureEmployerAsync(userId, model.CompanyName!.Trim());
            if (!result.Succeeded)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AccountController>>();
                logger.LogWarning("Could not create employer profile for {UserId}: {Error}", userId, result.Error);
            }
        }
    }

    private IActionResult SafeRedirect(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToDashboard();

    private IActionResult RedirectToDashboard()
    {
        if (IsAdministrator)
        {
            return RedirectToAction("Index", "Admin");
        }

        if (IsEmployer)
        {
            return RedirectToAction("Dashboard", "Employer");
        }

        if (IsJobSeeker)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        return RedirectToAction("Index", "Home");
    }
}
