using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using Application.Modules.HomeAssistant;
using Persistance;
using SweetHomeApi.Infrastructure.HomeAssistant;
using SweetHomeApi.Infrastructure.Realtime;
using SweetHomeApi.Registration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.RegisterRepositories();
builder.Services.RegisterApplicationServices();
builder.Services.AddSingleton<IHomeRealtimeBroadcaster, HomeRealtimeBroadcaster>();
builder.Services.Configure<HomeAssistantOptions>(builder.Configuration.GetSection("HomeAssistant"));
builder.Services.AddHttpClient<IHomeAssistantClient, HomeAssistantClient>();

// Добавляем сервисы Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    return uri.IsLoopback
                        || uri.Host == "87.242.102.150"
                        || uri.Host == "176.109.109.155";
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SweetHomeDbContext>(options =>
    options.UseNpgsql(connectionString)
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
);

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<SweetHomeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    var authCookieLifetime = TimeSpan.FromDays(365 * 50);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.MaxAge = authCookieLifetime;
    options.ExpireTimeSpan = authCookieLifetime;
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        // Возвращаем 401 (Unauthorized) вместо перенаправления
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        // Возвращаем 403 (Forbidden) вместо перенаправления
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});



var app = builder.Build();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");

// Включаем Swagger и его пользовательский интерфейс
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

app.MapControllers();
app.MapGet("/ws/home", async (
    HttpContext context,
    UserManager<IdentityUser> userManager,
    IHomeRealtimeBroadcaster realtimeBroadcaster,
    CancellationToken cancellationToken) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var userId = userManager.GetUserId(context.User);
    if (string.IsNullOrWhiteSpace(userId))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await realtimeBroadcaster.AddClientAsync(userId, webSocket, cancellationToken);
}).RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

    try
    {
        var context = services.GetRequiredService<SweetHomeDbContext>();
        context.Database.Migrate();
        ApplySmartHomeSchemaGuard(context);
        ApplyMoviesSchemaGuard(context);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Failed to apply database migrations.");
        throw;
    }
}


app.Run();

static void ApplySmartHomeSchemaGuard(SweetHomeDbContext context)
{
    context.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "SmartHomeScenarios" (
            "Id" text NOT NULL,
            "Name" text NOT NULL,
            "Icon" text NOT NULL,
            "ActionsJson" text NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "UserId" text NOT NULL,
            CONSTRAINT "PK_SmartHomeScenarios" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SmartHomeScenarios_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_SmartHomeScenarios_UserId"
            ON "SmartHomeScenarios" ("UserId");

        CREATE TABLE IF NOT EXISTS "SmartHomeAutomations" (
            "Id" text NOT NULL,
            "Name" text NOT NULL,
            "Enabled" boolean NOT NULL,
            "TriggerJson" text NOT NULL,
            "ConditionsJson" text NOT NULL,
            "ActionsJson" text NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "LastExecutedAt" timestamp with time zone NULL,
            "UserId" text NOT NULL,
            CONSTRAINT "PK_SmartHomeAutomations" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SmartHomeAutomations_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_SmartHomeAutomations_UserId"
            ON "SmartHomeAutomations" ("UserId");

        CREATE TABLE IF NOT EXISTS "SmartHomeEvents" (
            "Id" text NOT NULL,
            "Type" text NOT NULL,
            "Title" text NOT NULL,
            "Message" text NOT NULL,
            "EntityId" text NULL,
            "RoomId" text NULL,
            "PayloadJson" text NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UserId" text NOT NULL,
            CONSTRAINT "PK_SmartHomeEvents" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SmartHomeEvents_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_SmartHomeEvents_CreatedAt"
            ON "SmartHomeEvents" ("CreatedAt");

        CREATE INDEX IF NOT EXISTS "IX_SmartHomeEvents_UserId"
            ON "SmartHomeEvents" ("UserId");
        """);
}

static void ApplyMoviesSchemaGuard(SweetHomeDbContext context)
{
    context.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Movies" (
            "MovieId" text NOT NULL,
            "Title" text NOT NULL,
            "ContentType" text NOT NULL,
            "Rating" numeric NULL,
            "Genres" text[] NOT NULL,
            "Country" text NULL,
            "Comment" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "UserId" text NOT NULL,
            CONSTRAINT "PK_Movies" PRIMARY KEY ("MovieId"),
            CONSTRAINT "FK_Movies_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_Movies_CreatedAt"
            ON "Movies" ("CreatedAt");

        CREATE INDEX IF NOT EXISTS "IX_Movies_Rating"
            ON "Movies" ("Rating");

        CREATE INDEX IF NOT EXISTS "IX_Movies_Title"
            ON "Movies" ("Title");

        CREATE INDEX IF NOT EXISTS "IX_Movies_UpdatedAt"
            ON "Movies" ("UpdatedAt");

        CREATE INDEX IF NOT EXISTS "IX_Movies_UserId"
            ON "Movies" ("UserId");
        """);
}
