using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>Notification centre. Available to any signed-in account; always scoped to the caller.</summary>
[Authorize]
public class NotificationsController : ApplicationControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(ICurrentUserService currentUser, INotificationService notifications)
        : base(currentUser)
    {
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var results = await _notifications.ListAsync(userId, unreadOnly, page, 15, cancellationToken);

        return View(new NotificationListViewModel
        {
            UnreadOnly = unreadOnly,
            Page = page,
            Results = results
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _notifications.MarkAsReadAsync(id, userId, cancellationToken);
        if (!result.Succeeded)
        {
            FlashError(result.Error ?? "We could not update that notification.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = CurrentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _notifications.MarkAllAsReadAsync(userId, cancellationToken);
        if (!result.Succeeded)
        {
            FlashError(result.Error ?? "We could not update your notifications.");
        }

        return RedirectToAction(nameof(Index));
    }
}
