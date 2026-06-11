using Application.Modules.Widgets;
using Microsoft.AspNetCore.Authorization;
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


        [HttpPost("api/Account/Login")]
        [HttpPost("Account/Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            
            var user = await _userManager.FindByEmailAsync(loginRequest.Email);
            
            if (user == null || user.UserName == null)
            {
                return Unauthorized(new { Message = "Пользователь не найден" });
            }

            var result =
                await _signInManager.PasswordSignInAsync(user.UserName, loginRequest.Password, true, false);

            if (result.Succeeded)
                return Ok(new { Message = "Успешный вход", Name = user.UserName });

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
            
            var user = await _userManager.FindByEmailAsync(model.Email);
            
            if (user != null)
            {
                return BadRequest("Пользователь уже зарегестрирован");;
            }

            // Создаем нового пользователя
            var newUser = new IdentityUser
            {
                UserName = model.Name,
                Email = model.Email
            };

            // Пытаемся создать пользователя
            var result = await _userManager.CreateAsync(newUser, model.Password);
            if (result.Succeeded)
            {
                // После успешной регистрации, можно автоматически залогинить пользователя, если требуется
                await _widgetsService.AddDefaultWidgetForUser(newUser.Id);
                await _signInManager.SignInAsync(newUser, isPersistent: true);
                return Ok(new { message = "Пользователь успешно зарегистрирован" });
            }

            // Если возникли ошибки, возвращаем их
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return BadRequest(ModelState); // Возвращаем ошибки валидации
        }

        [HttpGet]
        [Authorize]
        [Route("api/Account/User")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var username = _userManager.GetUserName(User);
            
            return Ok(new { name = username });
        }


        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}
