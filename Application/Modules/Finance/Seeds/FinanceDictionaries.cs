namespace Application.Modules.Finance.Seeds;

/// <summary>Категория расходов: код, название, иконка Material Symbols, цвет для диаграммы, подсказка для модели.</summary>
public record FinanceCategoryInfo(FinanceCategory Code, string Name, string Icon, string Color, string Hint);

public static class FinanceDictionaries
{
    public static readonly IReadOnlyList<FinanceCategoryInfo> Categories =
    [
        new(FinanceCategory.GROCERIES, "Продукты", "shopping_basket", "#4CAF50",
            "еда и напитки из магазина; бытовая химия и хозтовары из того же чека — в HOME, корм — в PETS"),
        new(FinanceCategory.CAFE, "Кафе и рестораны", "restaurant", "#FF7043",
            "кафе, рестораны, кофейни, доставка готовой еды, фастфуд"),
        new(FinanceCategory.TRANSPORT, "Транспорт", "directions_bus", "#29B6F6",
            "такси, метро, автобус, электрички, каршеринг, самокаты"),
        new(FinanceCategory.CAR, "Автомобиль", "directions_car", "#5C6BC0",
            "топливо, мойка, парковка, запчасти, сервис, штрафы"),
        new(FinanceCategory.HOME, "Дом и хозтовары", "chair", "#8D6E63",
            "бытовая химия, хозтовары, посуда, мебель, ремонт, текстиль"),
        new(FinanceCategory.HEALTH, "Здоровье", "medication", "#EF5350",
            "аптека, лекарства, витамины, врачи, анализы"),
        new(FinanceCategory.BEAUTY, "Красота и гигиена", "spa", "#EC407A",
            "косметика, средства гигиены, парикмахерская, уход"),
        new(FinanceCategory.CLOTHES, "Одежда и обувь", "checkroom", "#AB47BC",
            "одежда, обувь, аксессуары"),
        new(FinanceCategory.PETS, "Питомцы", "pets", "#FFA726",
            "корм и лакомства для животных, ветеринар, зоотовары"),
        new(FinanceCategory.ELECTRONICS, "Техника", "devices", "#26A69A",
            "электроника, бытовая техника, гаджеты, комплектующие"),
        new(FinanceCategory.ENTERTAINMENT, "Развлечения", "theaters", "#7E57C2",
            "кино, театр, концерты, игры, хобби, книги, алкоголь в баре"),
        new(FinanceCategory.SERVICES, "Связь и подписки", "wifi", "#42A5F5",
            "мобильная связь, интернет, подписки, облака, прочие услуги"),
        new(FinanceCategory.UTILITIES, "ЖКХ", "bolt", "#FFCA28",
            "квартплата, электричество, вода, газ, отопление"),
        new(FinanceCategory.TRAVEL, "Путешествия", "flight", "#26C6DA",
            "билеты, отели, туры, визы, сувениры в поездке"),
        new(FinanceCategory.GIFTS, "Подарки", "redeem", "#F06292",
            "подарки, цветы, благотворительность"),
        new(FinanceCategory.EDUCATION, "Обучение", "school", "#66BB6A",
            "курсы, учебники, обучение"),
        new(FinanceCategory.OTHER, "Другое", "more_horiz", "#90A4AE",
            "всё, что не подходит под остальные категории")
    ];

    public static FinanceCategoryInfo Get(FinanceCategory category)
    {
        return Categories.First(x => x.Code == category);
    }
}
