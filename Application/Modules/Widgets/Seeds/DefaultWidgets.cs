namespace Application.Modules.Widgets.Seeds;

public static class DefaultMainWidgets
{
    public static List<MainWidget> GetDefaultWidgets(string userId)
    {
        return new List<MainWidget>
        {
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "home",
                Order = 1,
                Name = "Дом",
                Icon = "home",
                Size = 1,
                Hide = false,
                UserId = userId
            },
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "movies",
                Order = 2,
                Name = "Кино",
                Icon = "movie",
                Size = 2,
                Hide = false,
                UserId = userId
            },
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "books",
                Order = 3,
                Name = "Книги",
                Icon = "book",
                Size = 1,
                Hide = false,
                UserId = userId
            },
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "trips",
                Order = 4,
                Name = "Путешествия",
                Icon = "public",
                Size = 1,
                Hide = false,
                UserId = userId
            },
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "coins",
                Order = 6,
                Name = "Монеты",
                Icon = "toll",
                Size = 3,
                Hide = false,
                UserId = userId
            },
            new MainWidget
            {
                Id = Guid.NewGuid().ToString(),
                Alias = "health",
                Order = 7,
                Name = "Здоровье",
                Icon = "health_and_safety",
                Size = 3,
                Hide = false,
                UserId = userId
            }
        };
    }
}

