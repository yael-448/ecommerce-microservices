using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;

    public NotificationsController(IConnectionMultiplexer redis) => _redis = redis;

    [HttpGet("{email}")]
    public async Task<IActionResult> GetByEmail(string email)
    {
        var db = _redis.GetDatabase();
        var notifications = await db.ListRangeAsync($"notifications:{email}");
        return Ok(notifications.Select(n => n.ToString()).ToList());
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        var db = _redis.GetDatabase();
        var notification = await db.StringGetAsync($"notification:{orderId}");
        if (notification.IsNullOrEmpty) return NotFound();
        return Ok(notification.ToString());
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "NotificationService" });
}
