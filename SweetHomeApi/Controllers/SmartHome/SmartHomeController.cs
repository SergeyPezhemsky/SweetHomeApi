using Application.Modules.HomeAssistant;
using Application.Modules.SmartHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SweetHomeApi.Controllers.SmartHome.Dto;
using SweetHomeApi.Infrastructure.HomeAssistant;
using SweetHomeApi.Infrastructure.Realtime;

namespace SweetHomeApi.Controllers.SmartHome;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SmartHomeController(
    IHomeAssistantService homeAssistantService,
    ISmartHomeService smartHomeService,
    UserManager<IdentityUser> userManager,
    IHomeRealtimeBroadcaster realtimeBroadcaster) : ControllerBase
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

    [HttpPost("actions")]
    public async Task<IActionResult> ExecuteHomeAssistantAction(
        [FromBody] HomeAssistantActionDto action,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteHomeAssistantActionAsync(new HomeAssistantActionRequest
        {
            EntityId = action.EntityId,
            Action = action.Action,
            Value = action.Value
        }, cancellationToken);

        if (result is not NoContentResult)
            return result;

        var userId = userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var smartHomeEvent = await AddEventAsync(
                userId,
                "DEVICE_STATE_CHANGED",
                "Device state changed",
                $"Action '{action.Action}' was sent to '{action.EntityId}'.",
                action.EntityId,
                null,
                new { action.EntityId, action.Action, action.Value },
                cancellationToken);

            await realtimeBroadcaster.BroadcastAsync(
                userId,
                "DEVICE_STATE_CHANGED",
                MapToDto(smartHomeEvent),
                cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("widgets/{entityId}/command")]
    public async Task<IActionResult> ExecuteWidgetCommand(
        [FromRoute] string entityId,
        [FromBody] HomeAssistantCommandDto command,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteHomeAssistantActionAsync(new HomeAssistantActionRequest
        {
            EntityId = entityId,
            Action = command.Action,
            Value = command.Value
        }, cancellationToken);

        if (result is not NoContentResult)
            return result;

        var userId = userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var smartHomeEvent = await AddEventAsync(
                userId,
                "DEVICE_STATE_CHANGED",
                "Device state changed",
                $"Command '{command.Action}' was sent to '{entityId}'.",
                entityId,
                null,
                new { entityId, command.Action, command.Value },
                cancellationToken);

            await realtimeBroadcaster.BroadcastAsync(
                userId,
                "DEVICE_STATE_CHANGED",
                MapToDto(smartHomeEvent),
                cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var events = await smartHomeService.GetEventsAsync(userId, take, cancellationToken);

        return Ok(events.Select(MapToDto));
    }

    [HttpGet("scenarios")]
    public async Task<IActionResult> GetScenarios(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var scenarios = await smartHomeService.GetScenariosAsync(userId, cancellationToken);

        return Ok(scenarios.Select(MapToDto));
    }

    [HttpPost("scenarios")]
    public async Task<IActionResult> CreateScenario(
        [FromBody] SmartHomeScenarioDto scenario,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!TrySerializeJson(scenario.Actions, "[]", out var actionsJson, out var error))
            return BadRequest(new { message = error });

        var created = await smartHomeService.CreateScenarioAsync(new SmartHomeScenario
        {
            Id = scenario.Id ?? string.Empty,
            Name = scenario.Name,
            Icon = string.IsNullOrWhiteSpace(scenario.Icon) ? "panel-top" : scenario.Icon,
            ActionsJson = actionsJson,
            CreatedAt = default,
            UpdatedAt = default,
            UserId = userId
        }, userId, cancellationToken);

        return CreatedAtAction(nameof(GetScenarios), new { id = created.Id }, MapToDto(created));
    }

    [HttpPost("scenarios/{scenarioId}/execute")]
    public async Task<IActionResult> ExecuteScenario(
        [FromRoute] string scenarioId,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var scenario = await smartHomeService.GetScenarioAsync(scenarioId, userId, cancellationToken);
        if (scenario is null)
            return NotFound();

        IReadOnlyList<HomeAssistantActionDto> actions;
        try
        {
            actions = DeserializeActions(scenario.ActionsJson);
        }
        catch (JsonException ex)
        {
            return BadRequest(new
            {
                message = "Scenario actions must be a valid JSON array of Home Assistant actions.",
                details = ex.Message
            });
        }

        foreach (var action in actions)
        {
            var result = await ExecuteHomeAssistantActionAsync(new HomeAssistantActionRequest
            {
                EntityId = action.EntityId,
                Action = action.Action,
                Value = action.Value
            }, cancellationToken);

            if (result is not NoContentResult)
                return result;
        }

        var smartHomeEvent = await AddEventAsync(
            userId,
            "NEW_EVENT",
            "Scenario executed",
            $"Scenario '{scenario.Name}' was executed.",
            null,
            null,
            new { scenarioId = scenario.Id, scenario.Name },
            cancellationToken);

        await realtimeBroadcaster.BroadcastAsync(userId, "NEW_EVENT", MapToDto(smartHomeEvent), cancellationToken);

        return NoContent();
    }

    [HttpGet("automations")]
    public async Task<IActionResult> GetAutomations(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var automations = await smartHomeService.GetAutomationsAsync(userId, cancellationToken);

        return Ok(automations.Select(MapToDto));
    }

    [HttpPost("automations")]
    public async Task<IActionResult> CreateAutomation(
        [FromBody] SmartHomeAutomationDto automation,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!TryMapAutomation(automation, userId, out var model, out var error))
            return BadRequest(new { message = error });

        var created = await smartHomeService.CreateAutomationAsync(model, userId, cancellationToken);

        return CreatedAtAction(nameof(GetAutomations), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("automations/{automationId}")]
    public async Task<IActionResult> UpdateAutomation(
        [FromRoute] string automationId,
        [FromBody] SmartHomeAutomationDto automation,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        automation.Id = automationId;
        if (!TryMapAutomation(automation, userId, out var model, out var error))
            return BadRequest(new { message = error });

        var updated = await smartHomeService.UpdateAutomationAsync(model, userId, cancellationToken);
        if (updated is null)
            return NotFound();

        var smartHomeEvent = await AddEventAsync(
            userId,
            "NEW_EVENT",
            "Automation updated",
            $"Automation '{updated.Name}' was updated.",
            null,
            null,
            new { automationId = updated.Id, updated.Name, updated.Enabled },
            cancellationToken);

        await realtimeBroadcaster.BroadcastAsync(userId, "NEW_EVENT", MapToDto(smartHomeEvent), cancellationToken);

        return Ok(MapToDto(updated));
    }

    private async Task<IActionResult> ExecuteHomeAssistantActionAsync(
        HomeAssistantActionRequest action,
        CancellationToken cancellationToken)
    {
        try
        {
            await homeAssistantService.ExecuteActionAsync(action, cancellationToken);

            return NoContent();
        }
        catch (HomeAssistantActionException ex)
        {
            return BadRequest(new
            {
                message = "Home Assistant action is invalid.",
                details = ex.Message
            });
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

        var validationResult = await ValidateLayoutAsync(layout, cancellationToken);
        if (validationResult is not null)
            return validationResult;

        await smartHomeService.ReplaceLayoutAsync(MapToModel(layout, userId), userId, cancellationToken);

        var smartHomeEvent = await AddEventAsync(
            userId,
            "ROOM_UPDATED",
            "Home layout updated",
            "Smart home layout was updated.",
            null,
            null,
            new { rooms = layout.Rooms.Count, widgets = layout.Widgets.Count },
            cancellationToken);

        await realtimeBroadcaster.BroadcastAsync(userId, "ROOM_UPDATED", MapToDto(smartHomeEvent), cancellationToken);

        return NoContent();
    }

    private async Task<IActionResult?> ValidateLayoutAsync(
        SmartHomeLayoutDto layout,
        CancellationToken cancellationToken)
    {
        foreach (var widget in layout.Widgets)
        {
            if (string.IsNullOrWhiteSpace(widget.EntityId))
            {
                return BadRequest(new
                {
                    code = "ENTITY_ID_REQUIRED",
                    message = "Widget entityId is required."
                });
            }

            if (!IsValidJsonOrEmpty(widget.SettingsJson))
            {
                return BadRequest(new
                {
                    code = "INVALID_SETTINGS_JSON",
                    message = $"Widget '{widget.Id}' settingsJson must be valid JSON."
                });
            }
        }

        try
        {
            var entityIds = layout.Widgets
                .Select(widget => widget.EntityId)
                .Where(entityId => !string.IsNullOrWhiteSpace(entityId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var unknownEntityIds = await homeAssistantService.GetUnknownWidgetEntityIdsAsync(entityIds, cancellationToken);

            if (unknownEntityIds.Count > 0)
            {
                return BadRequest(new
                {
                    code = "UNKNOWN_ENTITY_ID",
                    message = "Layout contains unknown Home Assistant entityId.",
                    entityIds = unknownEntityIds
                });
            }
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

        return null;
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

    private static SmartHomeScenarioDto MapToDto(SmartHomeScenario scenario)
    {
        return new SmartHomeScenarioDto
        {
            Id = scenario.Id,
            Name = scenario.Name,
            Icon = scenario.Icon,
            Actions = ParseJsonElement(scenario.ActionsJson),
            CreatedAt = scenario.CreatedAt,
            UpdatedAt = scenario.UpdatedAt
        };
    }

    private static SmartHomeAutomationDto MapToDto(SmartHomeAutomation automation)
    {
        return new SmartHomeAutomationDto
        {
            Id = automation.Id,
            Name = automation.Name,
            Enabled = automation.Enabled,
            Trigger = ParseJsonElement(automation.TriggerJson),
            Conditions = ParseJsonElement(automation.ConditionsJson),
            Actions = ParseJsonElement(automation.ActionsJson),
            CreatedAt = automation.CreatedAt,
            UpdatedAt = automation.UpdatedAt,
            LastExecutedAt = automation.LastExecutedAt
        };
    }

    private static SmartHomeEventDto MapToDto(SmartHomeEvent smartHomeEvent)
    {
        return new SmartHomeEventDto
        {
            Id = smartHomeEvent.Id,
            Type = smartHomeEvent.Type,
            Title = smartHomeEvent.Title,
            Message = smartHomeEvent.Message,
            EntityId = smartHomeEvent.EntityId,
            RoomId = smartHomeEvent.RoomId,
            Payload = ParseJsonElement(smartHomeEvent.PayloadJson),
            CreatedAt = smartHomeEvent.CreatedAt
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

    private static bool TryMapAutomation(
        SmartHomeAutomationDto automation,
        string userId,
        out SmartHomeAutomation model,
        out string? error)
    {
        model = null!;
        error = null;

        if (!TrySerializeJson(automation.Trigger, "{}", out var triggerJson, out error))
            return false;

        if (!TrySerializeJson(automation.Conditions, "[]", out var conditionsJson, out error))
            return false;

        if (!TrySerializeJson(automation.Actions, "[]", out var actionsJson, out error))
            return false;

        model = new SmartHomeAutomation
        {
            Id = automation.Id ?? string.Empty,
            Name = automation.Name,
            Enabled = automation.Enabled,
            TriggerJson = triggerJson,
            ConditionsJson = conditionsJson,
            ActionsJson = actionsJson,
            CreatedAt = automation.CreatedAt ?? default,
            UpdatedAt = automation.UpdatedAt ?? default,
            LastExecutedAt = automation.LastExecutedAt,
            UserId = userId
        };

        return true;
    }

    private async Task<SmartHomeEvent> AddEventAsync(
        string userId,
        string type,
        string title,
        string message,
        string? entityId,
        string? roomId,
        object payload,
        CancellationToken cancellationToken)
    {
        var smartHomeEvent = new SmartHomeEvent
        {
            Id = string.Empty,
            Type = type,
            Title = title,
            Message = message,
            EntityId = entityId,
            RoomId = roomId,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        return await smartHomeService.AddEventAsync(smartHomeEvent, userId, cancellationToken);
    }

    private static bool IsValidJsonOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TrySerializeJson(
        JsonElement? value,
        string defaultJson,
        out string json,
        out string? error)
    {
        json = defaultJson;
        error = null;

        if (value is null || value.Value.ValueKind == JsonValueKind.Undefined)
            return true;

        try
        {
            json = value.Value.GetRawText();
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON payload is invalid: {ex.Message}";
            return false;
        }
    }

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<HomeAssistantActionDto> DeserializeActions(string actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
            return [];

        return JsonSerializer.Deserialize<List<HomeAssistantActionDto>>(
            actionsJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    }
}
