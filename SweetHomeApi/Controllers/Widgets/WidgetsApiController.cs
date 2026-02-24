using Application.Modules.Widgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Widgets.Dto;

namespace SweetHomeApi.Controllers.Widgets;

[ApiController]
[Route("api/[controller]")]
public class WidgetsController : ControllerBase
{
    private readonly IWidgetsService _widgetsService; // Используйте интерфейс для лучшей абстракции
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Инъекция зависимостей через конструктор.
    /// </summary>
    /// <param name="widgetsService">Сервис для работы с виджетами.</param>
    /// <param name="userManager">Менеджер пользователей.</param>
    public WidgetsController(IWidgetsService widgetsService, UserManager<IdentityUser> userManager)
    {
        _widgetsService = widgetsService;
        _userManager = userManager;
    }


    // Получить все виджеты
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
         var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        return Ok(await _widgetsService.GetUserWidgetsAsync(userId));
    }

    [HttpPut]
    [Authorize]
    public async Task<IActionResult> UpdateWidgets([FromBody] List<UpdateMainWidgetDto> updatedWidgets)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (updatedWidgets.Count == 0)
            return NoContent();

        var widgetsToUpdate = updatedWidgets.Select(widget => new Application.Modules.Widgets.MainWidget
        {
            Id = widget.Id,
            Alias = widget.Alias,
            Order = widget.Order,
            Name = widget.Name,
            Icon = widget.Icon,
            Size = widget.Size,
            Hide = widget.Hide,
            UserId = userId
        }).ToList();

        await _widgetsService.UpdateWidgetsAsync(widgetsToUpdate, userId);
        return NoContent();
    }
}
