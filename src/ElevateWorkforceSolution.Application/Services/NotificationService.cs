using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Database-backed notifications, always scoped to the requesting user.</summary>
public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IRepository<Notification> notifications,
        IUnitOfWork unitOfWork,
        ILogger<NotificationService> logger)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(
        string userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _notifications.Query().Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedDate = n.CreatedDate,
                RelatedEntityId = n.RelatedEntityId,
                RelatedEntityType = n.RelatedEntityType
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
        _notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<Result> MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);

        if (notification is null || notification.UserId != userId)
        {
            return Result.Failure("Notification not found.");
        }

        if (notification.IsRead)
        {
            return Result.Success();
        }

        notification.IsRead = true;
        _notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unread = await _notifications.Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return Result.Success();
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            _notifications.Update(notification);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return;
        }

        var notification = new Notification
        {
            UserId = request.UserId,
            Title = Truncate(request.Title, 200),
            Message = Truncate(request.Message, 1000),
            Type = request.Type,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = request.RelatedEntityType,
            CreatedDate = DateTime.UtcNow
        };

        await _notifications.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notification of type {Type} created for user {UserId}.",
            request.Type, request.UserId);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value ?? string.Empty : value[..maxLength];
}
