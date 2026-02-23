using Application.Modules.Widgets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Persistance;

public class SweetHomeDbContext(DbContextOptions<SweetHomeDbContext> options)
    : IdentityDbContext<IdentityUser>(options)

{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Конфигурация MainWidget
        builder.Entity<MainWidget>(entity =>
        {
            entity.HasKey(e => e.Id); // Установка первичного ключа

            // Связь MainWidget с таблицей пользователей через UserId
            entity
                .HasOne(e => e.User) // Навигационное свойство
                .WithMany() // Одному пользователю соответствует множество виджетов
                .HasForeignKey(e => e.UserId) // Внешний ключ
                .OnDelete(DeleteBehavior.Cascade); // Удаление виджетов при удалении пользователя
        });
    }


    public DbSet<MainWidget> MainWidgets { get; set; }
}
