using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace WpfDemoApp
{
    public class AppConfig
    {
        public string OutputPath { get; set; }
    }
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool isNotDownloading = true;
        private bool audioOnly = false;
        private AppConfig appConfig;

        private IProgress<DownloadProgress> progress;
        private IProgress<string> output;

        public MainWindow()
        {
            LoadConfig();
            this.YoutubeDL = new YoutubeDL() { YoutubeDLPath = "yt-dlp.exe" };
            this.DataContext = this;
            InitializeComponent();
            progress = new Progress<DownloadProgress>((p) => showProgress(p));
            output = new Progress<string>((s) => txtOutput.AppendText(s + Environment.NewLine));
        }

        private void LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                appConfig = new AppConfig()
                {
                    OutputPath = KnownFolders.GetPath(KnownFolder.Downloads)
                };
                File.WriteAllText("config.json", JsonConvert.SerializeObject(appConfig));
            }
            else
            {
                appConfig = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText("config.json"));
            }
        }

        public YoutubeDL YoutubeDL { get; }

        public bool IsNotDownloading
        {
            get => isNotDownloading;
            set
            {
                isNotDownloading = value;
                propertyChanged();
            }
        }

        public bool AudioOnly
        {
            get => audioOnly;
            set
            {
                audioOnly = value;
                propertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void propertyChanged([CallerMemberName] string property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text;
            IsNotDownloading = false;
            txtOutput.Clear();
            // Parse custom arguments
            OptionSet custom = OptionSet.FromString(txtOptions.Text.Split('\n'));
            RunResult<string> result;
            if (AudioOnly)
            {
                result = await YoutubeDL.RunAudioDownload(
                    url, AudioConversionFormat.Mp3, progress: progress,
                    output: output, overrideOptions: custom
                );
            }
            else
            {
                result = await YoutubeDL.RunVideoDownload(url, recodeFormat: VideoRecodeFormat.Mp4, progress: progress, output: output, overrideOptions: custom);
            }
            if (result.Success)
            {
                MessageBox.Show($"Successfully downloaded \"{url}\" to:\n\"{result.Data}\".", "YoutubeDLSharp");
            }
            else showErrorMessage(url, String.Join("\n", result.ErrorOutput));
            IsNotDownloading = true;
        }

        private void showProgress(DownloadProgress p)
        {
            txtState.Text = p.State.ToString();
            progDownload.Value = p.Progress;
            txtProgress.Text = $"speed: {p.DownloadSpeed} | left: {p.ETA}";
        }

        private async void InformationButton_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text;
            RunResult<VideoData> result = await YoutubeDL.RunVideoDataFetch(url);
            if (result.Success)
            {
                var infoWindow = new InformationWindow(result.Data);
                infoWindow.ShowDialog();
            }
            else showErrorMessage(url, String.Join("\n", result.ErrorOutput));
        }

        private void showErrorMessage(string url, string error)
            => MessageBox.Show($"Failed to process '{url}'. Output:\n\n{error}", "YoutubeDLSharp - ERROR",
                MessageBoxButton.OK, MessageBoxImage.Error);

        private void DownloadLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = appConfig.OutputPath;
            if (dlg.ShowDialog() == true)
            {
                MessageBox.Show($"Changed download folder to '{dlg.ResultPath}'");
                appConfig.OutputPath = dlg.ResultPath;
                File.WriteAllText("config.json", JsonConvert.SerializeObject(appConfig));
            }
        }
    }

    public enum KnownFolder
    {
        Contacts,
        Downloads,
        Favorites,
        Links,
        SavedGames,
        SavedSearches
    }

    public static class KnownFolders
    {
        private static readonly Dictionary<KnownFolder, Guid> _guids = new()
        {
            [KnownFolder.Contacts] = new("56784854-C6CB-462B-8169-88E350ACB882"),
            [KnownFolder.Downloads] = new("374DE290-123F-4565-9164-39C4925E467B"),
            [KnownFolder.Favorites] = new("1777F761-68AD-4D8A-87BD-30B759FA33DD"),
            [KnownFolder.Links] = new("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968"),
            [KnownFolder.SavedGames] = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"),
            [KnownFolder.SavedSearches] = new("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")
        };

        public static string GetPath(KnownFolder knownFolder)
        {
            return SHGetKnownFolderPath(_guids[knownFolder], 0);
        }

        [DllImport("shell32", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);
    }
}
