using Microsoft.AspNetCore.SignalR;
using SmartPay.Core.MultiTenancy;
using System.Security.Claims;

namespace SmartPay.Api.Notifications;

public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = TenantContext.Current.TenantId;
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(tenant));
        }
        await base.OnConnectedAsync();
    }

    internal static string Group(string tenantId) => $"tenant::{tenantId}";
}