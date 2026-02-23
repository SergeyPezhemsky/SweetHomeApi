namespace Application.Modules.Health.Seeds;

public static class DefaultHealthConfiguration
{
    public static List<HealthSection> GetSections()
    {
        return
        [
            new HealthSection { Id = "mood", Order = 1, Name = "Настроение", Hide = false, Type = "rating", Dictionary = true },
            new HealthSection { Id = "activity", Order = 2, Name = "Физическая активность", Hide = false, Type = "type", Dictionary = true },
            new HealthSection { Id = "weight", Order = 3, Name = "Вес", Hide = false, Type = "number", DefaultValue = "60", Dictionary = false },
            new HealthSection { Id = "blood-pressure", Order = 4, Name = "АД", Hide = false, Type = "big-number", DefaultValue = "36,6", Dictionary = false },
            new HealthSection { Id = "blood-sugar", Order = 5, Name = "Сахар", Hide = false, Type = "number", DefaultValue = "5,0", Dictionary = false },
            new HealthSection { Id = "water", Order = 6, Name = "Вода", Hide = false, Type = "number", DefaultValue = "1,5", Dictionary = false },
            new HealthSection { Id = "temperature", Order = 7, Name = "Температура", Hide = false, Type = "number", DefaultValue = "36,6", Dictionary = false },
            new HealthSection { Id = "other", Order = 8, Name = "Другое", Hide = false, Type = "type", Dictionary = true },
            new HealthSection { Id = "symptoms", Order = 9, Name = "Симптомы", Hide = false, Type = "type", Dictionary = true },
            new HealthSection { Id = "digestion", Order = 10, Name = "Пищеворение", Hide = false, Type = "type", Dictionary = true },
            new HealthSection { Id = "sex", Order = 11, Name = "Секс", Hide = false, Type = "type", Dictionary = true },
            new HealthSection { Id = "monthlies", Order = 12, Name = "Женский цикл", Hide = false, Type = "day-boolean", Dictionary = false }
        ];
    }

    public static List<HealthDictionaryItem> GetDictionary()
    {
        return
        [
            new HealthDictionaryItem { Id = "calm", Name = "Спокойствие", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "joy", Name = "Радость", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "high-energy", Name = "Много энергии", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "playfulness", Name = "Игривость", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "mood-swings", Name = "Перепады настроения", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "irritation", Name = "Раздражение", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "sadness", Name = "Грусть", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "anxiety", Name = "Тревога", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "depression", Name = "Подавленность", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "guilt", Name = "Чувство вины", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "obsessive-thoughts", Name = "Навязчивые мысли", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "low-energy", Name = "Мало энергии", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "apathy", Name = "Апатия", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "confusion", Name = "Растерянность", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "self-criticism", Name = "Жесткая самокритика", HealthSection = "mood", Value = false },
            new HealthDictionaryItem { Id = "mood", Name = "Радость", HealthSection = "mood", Value = false }
        ];
    }
}
