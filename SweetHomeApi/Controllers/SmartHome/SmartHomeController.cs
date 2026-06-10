using Application.Modules.HomeAssistant;
using Application.Modules.SmartHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.SmartHome.Dto;
using SweetHomeApi.Infrastructure.HomeAssistant;

namespace SweetHomeApi.Controllers.SmartHome;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SmartHomeController(
    IHomeAssistantService homeAssistantService,
    ISmartHomeService smartHomeService,
    UserManager<IdentityUser> userManager) : ControllerBase
{
    [HttpGet("widget-catalog")]
    public async Task<IActionResult> GetWidgetCatalog(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await homeAssistantService.GetWidgetCatalogAsync(cancellationToken));
        }
        catch (HomeAssistantConfigurationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Home Assistant integration is not configured.",
                details = ex.Message
            });
        }
        catch (HomeAssistantException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Home Assistant request failed.",
                details = ex.Message
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Home Assistant is unavailable.",
                details = ex.Message
            });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Home Assistant request timed out.",
                details = ex.Message
            });
        }
    }

    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var layout = await smartHomeService.GetLayoutAsync(userId, cancellationToken);

        return Ok(MapToDto(layout));
    }

    [HttpPut("layout")]
    public async Task<IActionResult> ReplaceLayout(
        [FromBody] SmartHomeLayoutDto layout,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        await smartHomeService.ReplaceLayoutAsync(MapToModel(layout, userId), userId, cancellationToken);

        return NoContent();
    }

    private static SmartHomeLayoutDto MapToDto(SmartHomeLayout layout)
    {
        return new SmartHomeLayoutDto
        {
            Rooms = layout.Rooms.Select(room => new SmartHomeRoomDto
            {
                Id = room.Id,
                Name = room.Name,
                Icon = room.Icon,
                Order = room.Order,
                Hide = room.Hide
            }).ToList(),
            Widgets = layout.Widgets.Select(widget => new SmartHomeWidgetDto
            {
                Id = widget.Id,
                EntityId = widget.EntityId,
                Type = widget.Type,
                Name = widget.Name,
                Icon = widget.Icon,
                Order = widget.Order,
                Size = widget.Size,
                Hide = widget.Hide,
                RoomId = widget.RoomId,
                SettingsJson = widget.SettingsJson
            }).ToList()
        };
    }

    private static SmartHomeLayout MapToModel(SmartHomeLayoutDto layout, string userId)
    {
        return new SmartHomeLayout
        {
            Rooms = layout.Rooms.Select(room => new SmartHomeRoom
            {
                Id = room.Id,
                Name = room.Name,
                Icon = room.Icon,
                Order = room.Order,
                Hide = room.Hide,
                UserId = userId
            }).ToList(),
            Widgets = layout.Widgets.Select(widget => new SmartHomeWidget
            {
                Id = widget.Id,
                EntityId = widget.EntityId,
                Type = widget.Type,
                Name = widget.Name,
                Icon = widget.Icon,
                Order = widget.Order,
                Size = widget.Size,
                Hide = widget.Hide,
                RoomId = widget.RoomId,
                SettingsJson = string.IsNullOrWhiteSpace(widget.SettingsJson) ? "{}" : widget.SettingsJson,
                UserId = userId
            }).ToList()
        };
    }
}
