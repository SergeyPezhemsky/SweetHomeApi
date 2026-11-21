using Application.Modules.Auth;
using Application.Modules.Widgets;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Persistance;
using SweetHomeApi.Controllers.Auth.Dto;

namespace SweetHomeApi.Controllers.Auth
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWidgetsService _widgetsService;
        private readonly SweetHomeDbContext _db;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IWidgetsService widgetsService,
            SweetHomeDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _widgetsService = widgetsService;
            _db = db;
        }


        [HttpPost("api/Account/Login")]
        [HttpPost("Account/Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            
            var user = await _userManager.FindByEmailAsync(loginRequest.Email);
            if (user?.UserName is null)
                return Unauthorized(new { message = "Неверный логин или пароль" });

            if (user.ApprovalStatus != ApprovalStatus.Approved)
            {
                var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, loginRequest.Password, false);
                if (!passwordCheck.Succeeded)
                    return Unauthorized(new { message = "Неверный логин или пароль" });

                var message = user.ApprovalStatus == ApprovalStatus.Rejected
                    ? "Администратор отклонил заявку на регистрацию."
                    : "Заявка на регистрацию ожидает одобрения администратора.";
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }

            var result =
                await _signInManager.PasswordSignInAsync(user, loginRequest.Password, true, false);

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
            
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var emailLock = "registration:" + _userManager.NormalizeEmail(model.Email);
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({emailLock}))");

            if (await _userManager.FindByEmailAsync(model.Email) is not null)
                return BadRequest(new { message = "Пользователь с этой почтой уже зарегистрирован." });

            var newUser = new ApplicationUser
            {
                UserName = model.Name,
                Email = model.Email,
                RegisteredAt = DateTime.UtcNow
            };

            IdentityResult result;
            try
            {
                result = await _userManager.CreateAsync(newUser, model.Password);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
            {
                return BadRequest(new { message = "Этот ник уже занят." });
            }

            if (result.Succeeded)
            {
                await _widgetsService.AddDefaultWidgetForUser(newUser.Id);
                await transaction.CommitAsync();
                return Ok(new { message = "Заявка отправлена. Вход станет доступен после одобрения администратором." });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return BadRequest(ModelState);
        }

        [Authorize]
        [HttpGet("api/Account/User")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            return user is null ? Unauthorized() : Profile(user);
        }

        [Authorize]
        [HttpPut("api/Account/User")]
        [Consumes("application/json")]
        public async Task<IActionResult> UpdateNickname([FromBody] UpdateNicknameDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var name = request.Name.Trim();
            if (name == user.UserName)
                return Profile(user);

            IdentityResult result;
            try
            {
                result = await _userManager.SetUserNameAsync(user, name);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
            {
                return Conflict(new { message = "Этот ник уже занят." });
            }

            if (!result.Succeeded)
                return ProfileError(result);

            await _signInManager.RefreshSignInAsync(user);
            return Profile(user);
        }

        [Authorize]
        [HttpPut("api/Account/Password")]
        [Consumes("application/json")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return ProfileError(result);

            await _signInManager.RefreshSignInAsync(user);
            return NoContent();
        }

        [HttpPost("api/Account/Logout")]
        [Consumes("application/json")]
        public async Task<IActionResult> Logout()
        {
            if (!Request.HasJsonContentType())
                return StatusCode(StatusCodes.Status415UnsupportedMediaType);

            await _signInManager.SignOutAsync();
            return NoContent();
        }

        private OkObjectResult Profile(ApplicationUser user) =>
            Ok(new { name = user.UserName, email = user.Email, isAdmin = user.IsAdmin });

        private IActionResult ProfileError(IdentityResult result)
        {
            var codes = result.Errors.Select(error => error.Code).ToHashSet();
            if (codes.Contains(nameof(IdentityErrorDescriber.ConcurrencyFailure)))
                return Conflict(new { message = "Данные профиля изменились. Повторите попытку." });

            if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
                return Conflict(new { message = "Этот ник уже занят." });

            if (codes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
                return BadRequest(new { message = "Используйте латинские буквы, цифры или символы -._@+." });

            if (codes.Contains(nameof(IdentityErrorDescriber.PasswordMismatch)))
                return BadRequest(new { message = "Текущий пароль неверен." });

            if (codes.Overlaps([
                    nameof(IdentityErrorDescriber.PasswordTooShort),
                    nameof(IdentityErrorDescriber.PasswordRequiresDigit),
                    nameof(IdentityErrorDescriber.PasswordRequiresLower),
                    nameof(IdentityErrorDescriber.PasswordRequiresUpper),
                    nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric)
                ]))
                return BadRequest(new { message = "Новый пароль не соответствует требованиям." });

            return BadRequest(new { errors = result.Errors.Select(error => error.Description).ToArray() });
        }


        public class LoginRequest
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;
        }
    }
}
