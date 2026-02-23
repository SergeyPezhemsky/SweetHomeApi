using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SweetHomeApi.Registration;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        // Загружаем сборку, в которой находятся службы
        var applicationAssembly = Assembly.Load("Application");

        var a = applicationAssembly.GetTypes();
        // Находим все классы, которые реализуют интерфейсы и находятся в пространстве имен Application.Services
        var serviceTypes = applicationAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Namespace != null && type.FullName!.EndsWith("Service"));

        foreach (var implementationType in serviceTypes)
        {
            // Для каждого класса ищем интерфейс с именем I{ClassName}
            var interfaceType = implementationType.GetInterface($"I{implementationType.Name}");
            if (interfaceType != null)
            {
                // Регистрируем интерфейс и класс с жизненным циклом Scoped
                services.AddScoped(interfaceType, implementationType);
            }
        }

        return services;
    }
}