using Application.Modules.Widgets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Auth.Dto;

namespace SweetHomeApi.Controllers.Auth
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWidgetsService _widgetsService; // Используйте интерфейс для лучшей абстракции

        // Инициализация зависимостей через конструктор
        public AccountController(
            SignInManager<IdentityUser> signInManager, 
            UserManager<IdentityUser> userManager,
            IWidgetsService widgetsService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _widgetsService = widgetsService;
        }


        [HttpPost]
        [Route("Account/Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result =
                await _signInManager.PasswordSignInAsync(loginRequest.Email, loginRequest.Password, false, false);

            if (result.Succeeded)
                return Ok(new { Message = "Успешный вход" });

            return Unauthorized(new { Message = "Неверный логин или пароль" });
        }
        
        [HttpPost]
        [Route("api/Account/Register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Создаем нового пользователя
            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            // Пытаемся создать пользователя
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // После успешной регистрации, можно автоматически залогинить пользователя, если требуется
                await _widgetsService.AddDefaultWidgetForUser(user.Id);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok(new { message = "Пользователь успешно зарегистрирован" });
            }

            // Если возникли ошибки, возвращаем их
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return BadRequest(ModelState); // Возвращаем ошибки валидации
        }


        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}