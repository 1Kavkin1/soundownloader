using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SongDownloader.Services;

namespace SongDownloader;

public partial class MainWindow : Window
{
    private string? _folder;

    public MainWindow()
    {
        InitializeComponent();
        DownloadProgress.Maximum = Songs.Urls.Length;
        CounterText.Text = $"0/{Songs.Urls.Length}";
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();

        if (dialog.ShowDialog() != true)
            return;

        _folder = dialog.FolderName;
        PathTextBox.Text = _folder;
    }

    private async void SequentialButton_Click(object sender, RoutedEventArgs e)
        => await RunDownloadAsync(useThreads: false);

    private async void ThreadsButton_Click(object sender, RoutedEventArgs e)
        => await RunDownloadAsync(useThreads: true);

    private async Task RunDownloadAsync(bool useThreads)
    {
        if (string.IsNullOrEmpty(_folder))
        {
            MessageBox.Show("Спочатку оберіть папку для збереження.", "Увага",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string folder = _folder;
        string[] urls = Songs.Urls;
        int total = urls.Length;

        SetBusy(true);
        LogList.Items.Clear();
        DownloadProgress.Value = 0;
        CounterText.Text = $"0/{total}";

        // Progress створюється в UI-потоці, тому Report() з робочих потоків
        // безпечно оновлює елементи інтерфейсу
        IProgress<int> progress = new Progress<int>(done =>
        {
            DownloadProgress.Value = done;
            CounterText.Text = $"{done}/{total}";
        });

        IProgress<string> log = new Progress<string>(message =>
        {
            LogList.Items.Add(message);
        });

        var failed = new ConcurrentBag<string>();
        int completed = 0;

        void Download(string url)
        {
            try
            {
                FileDownloader.Download(url, folder);
                log.Report($"Готово: {FileDownloader.GetFileName(url)}");
            }
            catch (Exception ex)
            {
                failed.Add(url);
                log.Report($"Помилка: {url}\n    {ex.Message}");
            }
            finally
            {
                progress.Report(Interlocked.Increment(ref completed));
            }
        }

        var stopwatch = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            if (useThreads)
            {
                // Один потік на одну пісню: усі завантаження йдуть одночасно
                var threads = urls
                    .Select(url => new Thread(() => Download(url)) { IsBackground = true })
                    .ToList();

                threads.ForEach(t => t.Start());
                threads.ForEach(t => t.Join());
            }
            else
            {
                // Пісні по черзі, одна за одною
                foreach (var url in urls)
                    Download(url);
            }
        });

        stopwatch.Stop();
        double seconds = stopwatch.Elapsed.TotalSeconds;

        if (useThreads)
            ThreadsTimeText.Text = $"Потоками: {seconds:F2} с";
        else
            SequentialTimeText.Text = $"Послідовно: {seconds:F2} с";

        SetBusy(false);

        string mode = useThreads ? "Потоками" : "Послідовно";
        MessageBox.Show(
            $"Успішно: {total - failed.Count} з {total}\nЧас: {seconds:F2} с",
            mode,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SetBusy(bool busy)
    {
        SelectFolderButton.IsEnabled = !busy;
        SequentialButton.IsEnabled = !busy;
        ThreadsButton.IsEnabled = !busy;
    }
}
