using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class OutputFolderDefaults
{
    public static string Resolve(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : configured;
}
