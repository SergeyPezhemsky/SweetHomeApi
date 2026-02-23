using System.Globalization;
using Application.Modules.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SweetHomeApi.Controllers.Health.Dto;

namespace SweetHomeApi.Controllers.Health;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IHealthService healthService, UserManager<IdentityUser> userManager) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get([FromQuery] string? data)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!TryParseDate(data, out var date))
            return BadRequest("Неверный формат даты. Используйте dd.MM.yyyy или yyyy-MM-dd.");

        var sections = await healthService.GetSectionsAsync();
        var health = await healthService.GetByDateAsync(userId, date);

        return Ok(new HealthResponseDto
        {
            HealthSections = sections.Select(x => new HealthSectionDto
            {
                Id = x.Id,
                Order = x.Order,
                Name = x.Name,
                Hide = x.Hide,
                Type = x.Type,
                Dictionary = x.Dictionary,
                DefaultValue = x.DefaultValue
            }).ToList(),
            Health = new HealthDto
            {
                Data = health.Date.ToString("dd.MM.yyyy"),
                HealthDictionary = health.HealthDictionary.Select(x => new HealthDictionaryItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    HealthSection = x.HealthSection,
                    Value = x.Value
                }).ToList(),
                Weight = health.Weight,
                BloodPressure = health.BloodPressure,
                BloodSugar = health.BloodSugar,
                Water = health.Water,
                Temperature = health.Temperature,
                Monthlies = health.Monthlies
            }
        });
    }

    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Put([FromBody] UpdateHealthDto dto)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!TryParseDate(dto.Data, out var date))
            return BadRequest("Неверный формат даты. Используйте dd.MM.yyyy или yyyy-MM-dd.");

        var existingData = await healthService.GetByDateAsync(userId, date);

        var data = new HealthDayData
        {
            Date = date,
            HealthDictionary = dto.HealthDictionary is null
                ? existingData.HealthDictionary
                : dto.HealthDictionary.Select(x => new Application.Modules.Health.HealthDictionaryItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    HealthSection = x.HealthSection,
                    Value = x.Value
                }).ToList(),
            Weight = dto.Weight ?? existingData.Weight,
            BloodPressure = dto.BloodPressure ?? existingData.BloodPressure,
            BloodSugar = dto.BloodSugar ?? existingData.BloodSugar,
            Water = dto.Water ?? existingData.Water,
            Temperature = dto.Temperature ?? existingData.Temperature,
            Monthlies = dto.Monthlies ?? existingData.Monthlies
        };

        await healthService.UpsertAsync(userId, data);
        return NoContent();
    }

    private static bool TryParseDate(string? input, out DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            return true;
        }

        return DateOnly.TryParseExact(input, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
