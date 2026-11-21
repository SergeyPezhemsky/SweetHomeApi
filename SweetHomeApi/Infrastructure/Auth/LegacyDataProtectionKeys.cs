using System.Formats.Tar;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Persistance;

namespace SweetHomeApi.Infrastructure.Auth;

internal static class LegacyDataProtectionKeys
{
    public static int Import(SweetHomeDbContext context, Stream archive)
    {
        using var transaction = context.Database.BeginTransaction();
        // ASP.NET Core's table has no unique constraint on FriendlyName.
        context.Database.ExecuteSqlRaw("""LOCK TABLE "DataProtectionKeys" IN SHARE ROW EXCLUSIVE MODE""");
        var names = context.DataProtectionKeys.Select(key => key.FriendlyName).ToHashSet(StringComparer.Ordinal);

        using var reader = new TarReader(archive, leaveOpen: true);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is not { } xmlStream ||
                !string.Equals(Path.GetExtension(entry.Name), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(entry.Name);
            if (names.Add(name))
            {
                context.DataProtectionKeys.Add(new DataProtectionKey
                {
                    FriendlyName = name,
                    Xml = XElement.Load(xmlStream).ToString(SaveOptions.DisableFormatting)
                });
            }
        }

        var imported = context.SaveChanges();
        transaction.Commit();
        return imported;
    }
}
