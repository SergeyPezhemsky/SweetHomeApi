using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SweetHomeApi.Registration;

public static class RepositoryRegistration
{
    public static IServiceCollection RegisterRepositories(this IServiceCollection services)
    {
        // Загружаем сборку с репозиториями (предполагается, что репозитории находятся в одной общей сборке)
        var repositoryAssembly = Assembly.Load("Persistance");

        // Получаем все типы из этой сборки
        var repositoryTypes = repositoryAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Repository"));
        
        foreach (var implementationType in repositoryTypes)
        {
            // Ищем интерфейс, который реализует класс
            var interfaceType = implementationType.GetInterface($"I{implementationType.Name}");
            if (interfaceType != null)
            {
                // Регистрируем интерфейс и его реализацию
                services.AddScoped(interfaceType, implementationType);
            }
        }

        return services;
    }
}