using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class DownloadFolderDefaults
{
    public static string Resolve(string? configured) => string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads") : configured;
}