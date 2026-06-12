namespace Application.Modules.Movies.Seeds;

public static class MovieDictionaries
{
    public static readonly List<MovieContentTypeItem> ContentTypes =
    [
        new() { Code = "MOVIE", Name = "фильм" },
        new() { Code = "CARTOON", Name = "мультфильм" },
        new() { Code = "SERIES", Name = "сериал" }
    ];

    public static readonly List<string> Genres =
    [
        "Комедия",
        "Приключения",
        "Фантастика",
        "Аниме",
        "Биография",
        "Боевик",
        "Вестерн",
        "Военный",
        "Детектив",
        "Детский",
        "Документальный",
        "Драма",
        "Исторический",
        "Криминал",
        "Мелодрама",
        "Мистика",
        "Семейный",
        "Триллер",
        "Ужасы",
        "Фэнтези"
    ];

    public static readonly List<string> Countries =
    [
        "США",
        "Россия",
        "Япония",
        "Франция",
        "Германия",
        "Великобритания",
        "Южная Корея",
        "Китай"
    ];
}
