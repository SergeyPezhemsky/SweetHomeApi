using Application.Modules.HomeAssistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Infrastructure.HomeAssistant;

namespace SweetHomeApi.Controllers.SmartHome;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SmartHomeController(IHomeAssistantService homeAssistantService) : ControllerBase
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
}
