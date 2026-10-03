using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ShutdownTimer;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _countdownTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(200)
    };

    private Button[] _presetButtons = [];
    private readonly Brush _presetIdleBackground = new SolidColorBrush(Color.FromRgb(33, 26, 32));
    private readonly Brush _presetIdleForeground = new SolidColorBrush(Color.FromRgb(216, 209, 213));
    private readonly Brush _presetActiveBackground = new SolidColorBrush(Color.FromRgb(243, 238, 232));
    private readonly Brush _presetActiveForeground = new SolidColorBrush(Color.FromRgb(23, 20, 25));

    private DateTime? _shutdownAt;
    private bool _videoPaused;
    private bool _videoAvailable;
    private bool _suppressInputUpdate;
    private bool _uiReady;

    public MainWindow()
    {
        InitializeComponent();

        _presetButtons =
        [
            Preset5,
            Preset10,
            Preset30,
            Preset45,
            Preset60,
            Preset120
        ];

        _uiReady = true;
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
        PreviewCurrentDuration();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadBackgroundVideo();
        UpdatePresetSelection();
    }

    private void LoadBackgroundVideo()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "background.mp4"),
            Path.Combine(baseDir, "Assets", "background.mp4")
        ];

        string? path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            _videoAvailable = false;
            BackgroundVideo.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            BackgroundVideo.Source = new Uri(path, UriKind.Absolute);
            BackgroundVideo.Volume = 0;
            BackgroundVideo.IsMuted = true;
            BackgroundVideo.Play();
            _videoAvailable = true;
        }
        catch
        {
            _videoAvailable = false;
            BackgroundVideo.Visibility = Visibility.Collapsed;
        }
    }

    private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (!_videoAvailable || _videoPaused)
            return;

        BackgroundVideo.Position = TimeSpan.Zero;
        BackgroundVideo.Play();
    }

    private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _videoAvailable = false;
        BackgroundVideo.Visibility = Visibility.Collapsed;
    }

    private void VideoProximityZone_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_videoAvailable)
            return;

        VideoToggleButton.IsHitTestVisible = true;
        AnimateOpacity(VideoToggleButton, 1, 150);
    }

    private void VideoProximityZone_MouseLeave(object sender, MouseEventArgs e)
    {
        AnimateOpacity(VideoToggleButton, 0, 170);
        VideoToggleButton.IsHitTestVisible = false;
    }

    private void VideoToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_videoAvailable)
            return;

        if (_videoPaused)
        {
            BackgroundVideo.Play();
            _videoPaused = false;
            VideoToggleButton.Content = "✓";
            ShowToast("Background berjalan");
        }
        else
        {
            BackgroundVideo.Pause();
            _videoPaused = true;
            VideoToggleButton.Content = "▶";
            ShowToast("Background dibekukan");
        }
    }

    private static void AnimateOpacity(UIElement element, double target, int milliseconds)
    {
        element.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new QuadraticEase()
            });
    }

    private void ScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        int seconds = GetTotalSeconds();

        if (seconds <= 0)
        {
            ShowToast("Masukkan durasi dulu");
            return;
        }

        RunShutdown("/a", ignoreErrors: true);

        if (!RunShutdown($"/s /t {seconds}", ignoreErrors: false))
            return;

        _shutdownAt = DateTime.Now.AddSeconds(seconds);
        _countdownTimer.Start();
        ScheduleButton.Content = "PERBARUI JADWAL";
        StateText.Text = "SCHEDULED";
        StateDot.Fill = new SolidColorBrush(Color.FromRgb(243, 238, 232));

        UpdateCountdown();
        ShowToast("Shutdown dijadwalkan");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        bool hadLocalSchedule = _shutdownAt is not null;
        bool cancelled = RunShutdown("/a", ignoreErrors: true);

        _shutdownAt = null;
        _countdownTimer.Stop();
        ScheduleButton.Content = "JADWALKAN SHUTDOWN";

        PreviewCurrentDuration();
        ShowToast(hadLocalSchedule || cancelled ? "Shutdown dibatalkan" : "Tidak ada shutdown aktif");
    }

    private bool RunShutdown(string arguments, bool ignoreErrors)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
                throw new InvalidOperationException("shutdown.exe tidak dapat dijalankan.");

            process.WaitForExit(3000);

            if (process.ExitCode != 0 && !ignoreErrors)
            {
                MessageBox.Show(
                    $"Windows menolak perintah shutdown (kode {process.ExitCode}).",
                    "Shutdown Timer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            if (!ignoreErrors)
            {
                MessageBox.Show(
                    $"Gagal menjalankan perintah shutdown.\n\n{ex.Message}",
                    "Shutdown Timer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return false;
        }
    }

    private void UpdateCountdown()
    {
        if (_shutdownAt is null)
        {
            PreviewCurrentDuration();
            return;
        }

        TimeSpan remaining = _shutdownAt.Value - DateTime.Now;

        if (remaining <= TimeSpan.Zero)
        {
            CountdownText.Text = "00:00:00";
            StatusText.Text = "Sedang shutdown";
            _countdownTimer.Stop();
            return;
        }

        CountdownText.Text = FormatDuration(remaining);
        StatusText.Text = $"Mati sekitar {_shutdownAt.Value:HH:mm:ss}";
    }

    private void PreviewCurrentDuration()
    {
        if (_shutdownAt is not null)
            return;

        CountdownText.Text = FormatDuration(TimeSpan.FromSeconds(GetTotalSeconds()));
        StatusText.Text = "Belum dijadwalkan";
        StateText.Text = "READY";
        StateDot.Fill = new SolidColorBrush(Color.FromRgb(217, 70, 104));
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = Regex.IsMatch(e.Text, "[^0-9]");
    }

    private void TimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _suppressInputUpdate)
            return;

        if (_shutdownAt is null)
            PreviewCurrentDuration();

        UpdatePresetSelection();
    }

    private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        int max = textBox == HoursBox ? 99 : 59;
        SetTextBoxValue(textBox, Math.Clamp(ParseValue(textBox), 0, max));

        if (_shutdownAt is null)
            PreviewCurrentDuration();

        UpdatePresetSelection();
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
            return;

        string[] pieces = tag.Split(':');
        if (pieces.Length != 2 || !int.TryParse(pieces[1], out int step))
            return;

        TextBox target = pieces[0] switch
        {
            "hours" => HoursBox,
            "minutes" => MinutesBox,
            "seconds" => SecondsBox,
            _ => SecondsBox
        };

        int max = target == HoursBox ? 99 : 59;
        SetTextBoxValue(target, Math.Clamp(ParseValue(target) + step, 0, max));

        if (_shutdownAt is null)
            PreviewCurrentDuration();

        UpdatePresetSelection();
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string tag ||
            !int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
            return;

        SetDuration(seconds);

        if (_shutdownAt is null)
            PreviewCurrentDuration();

        UpdatePresetSelection();
    }

    private void SetDuration(int totalSeconds)
    {
        _suppressInputUpdate = true;
        try
        {
            HoursBox.Text = (totalSeconds / 3600).ToString(CultureInfo.InvariantCulture);
            MinutesBox.Text = ((totalSeconds % 3600) / 60).ToString(CultureInfo.InvariantCulture);
            SecondsBox.Text = (totalSeconds % 60).ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressInputUpdate = false;
        }
    }

    private int GetTotalSeconds()
    {
        int hours = Math.Clamp(ParseValue(HoursBox), 0, 99);
        int minutes = Math.Clamp(ParseValue(MinutesBox), 0, 59);
        int seconds = Math.Clamp(ParseValue(SecondsBox), 0, 59);
        return hours * 3600 + minutes * 60 + seconds;
    }

    private static int ParseValue(TextBox? box)
        => box is not null &&
           int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;

    private void SetTextBoxValue(TextBox box, int value)
    {
        _suppressInputUpdate = true;
        box.Text = value.ToString(CultureInfo.InvariantCulture);
        _suppressInputUpdate = false;
    }

    private void UpdatePresetSelection()
    {
        int current = GetTotalSeconds();

        foreach (Button button in _presetButtons)
        {
            bool active =
                button.Tag is string tag &&
                int.TryParse(tag, out int presetSeconds) &&
                presetSeconds == current;

            button.Background = active ? _presetActiveBackground : _presetIdleBackground;
            button.Foreground = active ? _presetActiveForeground : _presetIdleForeground;
        }
    }

    private static string FormatDuration(TimeSpan value)
    {
        int totalHours = (int)Math.Floor(Math.Max(0, value.TotalHours));
        return $"{totalHours:00}:{Math.Max(0, value.Minutes):00}:{Math.Max(0, value.Seconds):00}";
    }

    private async void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));

        await Task.Delay(1400);

        ToastBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
