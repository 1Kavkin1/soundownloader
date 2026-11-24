using System;
using System.IO;
using System.Net.Http;

namespace SongDownloader.Services;

/// <summary>Копіює файл із мережі на диск. Виконується в тому потоці, який його викликав.</summary>
public static class FileDownloader
{
    // HttpClient потокобезпечний, тому один екземпляр на всі потоки
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static string GetFileName(string url)
        => Path.GetFileName(new Uri(url).LocalPath);

    public static void Download(string url, string folder)
    {
        string path = Path.Combine(folder, GetFileName(url));

        using HttpResponseMessage response = Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .GetAwaiter()
            .GetResult();

        response.EnsureSuccessStatusCode();

        using Stream source = response.Content.ReadAsStream();
        using FileStream target = File.Create(path);
        source.CopyTo(target);
    }
}
