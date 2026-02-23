using Application.Modules.Widgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
    public IActionResult UpdateWidgets([FromBody] List<MainWidget> updatedWidgets)
    {

        return NoContent();
    }
}
