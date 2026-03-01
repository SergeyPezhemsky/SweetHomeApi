namespace Application.Modules.Health.Seeds;

public static class DefaultHealthConfiguration
{
    public static List<HealthSection> GetSections()
    {
        return
        [
            new HealthSection { Id = "mood", Order = 5, Name = "Настроение", Hide = false, Type = "rating", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "activity", Order = 6, Name = "Физическая активность", Hide = false, Type = "type", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "weight", Order = 1, Name = "Вес", Hide = false, Type = "number", Dictionary = false, AverageValue = true },
            new HealthSection { Id = "blood-pressure", Order = 2, Name = "АД", Hide = false, Type = "big-number", Dictionary = false, AverageValue = true },
            new HealthSection { Id = "blood-sugar", Order = 3, Name = "Сахар", Hide = false, Type = "number", Dictionary = false, AverageValue = true },
            new HealthSection { Id = "water", Order = 4, Name = "Вода", Hide = false, Type = "number", Dictionary = false, AverageValue = false },
            new HealthSection { Id = "temperature", Order = 7, Name = "Температура", Hide = false, Type = "number", Dictionary = false, AverageValue = true },
            new HealthSection { Id = "other", Order = 8, Name = "Другое", Hide = false, Type = "type", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "symptoms", Order = 9, Name = "Симптомы", Hide = false, Type = "type", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "digestion", Order = 10, Name = "Пищеворение", Hide = false, Type = "type", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "sex", Order = 11, Name = "Секс", Hide = false, Type = "type", Dictionary = true, AverageValue = false },
            new HealthSection { Id = "monthlies", Order = 12, Name = "Женский цикл", Hide = false, Type = "day-boolean", Dictionary = false, AverageValue = false }
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
            new HealthDictionaryItem { Id = "no-training", Name = "Тренировки не было", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "yoga", Name = "Йога", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "gym", Name = "Тренажерный зал", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "aerobics", Name = "Аэробика и танцы", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "swimming", Name = "Плавание", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "running", Name = "Бег", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "cycling", Name = "Велосипед", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "walking", Name = "Ходьба", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "team-sports", Name = "Командный спорт", HealthSection = "activity", Value = false },
            new HealthDictionaryItem { Id = "stress", Name = "Стресс", HealthSection = "other", Value = false },
            new HealthDictionaryItem { Id = "alcohol", Name = "Алкоголь", HealthSection = "other", Value = false },
            new HealthDictionaryItem { Id = "meditation", Name = "Медитация", HealthSection = "other", Value = false },
            new HealthDictionaryItem { Id = "disease", Name = "Болезнь или травма", HealthSection = "other", Value = false },
            new HealthDictionaryItem { Id = "abdominal-pain", Name = "Боль в животе", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "lower-abdominal-pain", Name = "Боли внизу живота", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "headache", Name = "Головная боль", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "acne", Name = "Прыщи", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "back-pain", Name = "Боль в спине", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "fatigue", Name = "Физическаая усталость", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "increased-appetite", Name = "Повышенный аппетит", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "insomnia", Name = "Бессонница", HealthSection = "symptoms", Value = false },
            new HealthDictionaryItem { Id = "nausea", Name = "Тошнота", HealthSection = "digestion", Value = false },
            new HealthDictionaryItem { Id = "bloating", Name = "Вздутие", HealthSection = "digestion", Value = false },
            new HealthDictionaryItem { Id = "diarrhea", Name = "Диарея", HealthSection = "digestion", Value = false },
            new HealthDictionaryItem { Id = "constipation", Name = "Запор", HealthSection = "digestion", Value = false },
            new HealthDictionaryItem { Id = "high-libido", Name = "Сильное желание", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "medium-libido", Name = "Среднее желание", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "low-libido", Name = "Слабое желание", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "no-sex", Name = "Секса не было", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "sex-with-protection", Name = "Секс с защитой", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "sex-without-protection", Name = "Секс без защиты", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "masturbation", Name = "Мастурбация", HealthSection = "sex", Value = false },
            new HealthDictionaryItem { Id = "orgasm", Name = "Оргазм", HealthSection = "sex", Value = false }
        ];
    }
}
