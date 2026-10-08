using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using PetroTrans.Api;

namespace PetroTrans.Desktop;

public partial class MainWindow : Window
{
    private WebApplication? _api;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        FitToWorkingArea();
    }

    private void FitToWorkingArea()
    {
        var work = SystemParameters.WorkArea;
        MinWidth = Math.Min(640, Math.Max(480, work.Width - 16));
        MinHeight = Math.Min(480, Math.Max(400, work.Height - 16));
        Width = Math.Max(MinWidth, work.Width - 16);
        Height = Math.Max(MinHeight, work.Height - 16);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = work.Left + 8;
        Top = work.Top + 8;
        WindowState = WindowState.Normal;
        SizeChanged += (_, _) => ApplyContentScale();
    }

    private void ApplyContentScale()
    {
        if (WebView.CoreWebView2 is null || ActualWidth < 50 || ActualHeight < 50)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var zoom = Math.Min(ActualWidth / 1366.0, ActualHeight / 768.0);
        if (dpi > 1.05)
        {
            zoom /= dpi;
        }

        zoom = Math.Clamp(zoom, 0.5, 1.0);
        if (Math.Abs(WebView.ZoomFactor - zoom) > 0.01)
        {
            WebView.ZoomFactor = zoom;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _cts = new CancellationTokenSource();
            var port = GetFreeLoopbackPort();
            _api = PetroTransApiHost.Create([], port);
            await PetroTransApiHost.InitializeDatabaseAsync(_api, _cts.Token);
            await _api.StartAsync(_cts.Token);

            var url = $"http://127.0.0.1:{port}/";
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PetroTrans",
                "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await WebView.EnsureCoreWebView2Async(env);
            ConfigureWebView();
            ApplyContentScale();
            WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            WebView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"تعذر تشغيل التطبيق.{Environment.NewLine}{ex.Message}";
            StatusText.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
        }
    }

    private void ConfigureWebView()
    {
        var settings = WebView.CoreWebView2.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreHostObjectsAllowed = false;

        WebView.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = true;
        WebView.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            if (IsLoopbackAppUri(args.Uri) && !IsBlankUri(args.Uri))
            {
                WebView.CoreWebView2.Navigate(args.Uri);
            }
        };
        WebView.CoreWebView2.NavigationStarting += (_, args) =>
        {
            if (!IsLoopbackAppUri(args.Uri))
            {
                args.Cancel = true;
            }
        };
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!PrintHostMessage.TryParse(args.WebMessageAsJson, out var command) || command is null)
        {
            return;
        }

        try
        {
            if (command.Type == "pdf")
            {
                await SavePdfAsync(command.FileName);
                return;
            }

            await WebView.CoreWebView2.ExecuteScriptAsync("window.print()");
        }
        catch (Exception ex)
        {
            var fallback = MessageBox.Show(
                this,
                $"تعذر فتح حوار الطباعة.{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}حفظ PDF بدلاً من ذلك؟",
                "طباعة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (fallback == MessageBoxResult.Yes)
            {
                try
                {
                    await SavePdfAsync(command.FileName);
                }
                catch (Exception pdfEx)
                {
                    MessageBox.Show(this, $"تعذر حفظ PDF.{Environment.NewLine}{pdfEx.Message}", "طباعة", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async Task SavePdfAsync(string? fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "حفظ PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = string.IsNullOrWhiteSpace(fileName) ? "petrotrans.pdf" : fileName,
            AddExtension = true,
            DefaultExt = ".pdf",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var ok = await WebView.CoreWebView2.PrintToPdfAsync(dialog.FileName);
        if (!ok)
        {
            MessageBox.Show(this, "تعذر إنشاء ملف PDF. تأكد أن الطابعة أو الحفظ متاح.", "طباعة", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        Dispatcher.Invoke(() =>
        {
            if (args.IsSuccess)
            {
                StatusText.Visibility = Visibility.Collapsed;
                WebView.Visibility = Visibility.Visible;
                ApplyContentScale();
            }
            else
            {
                StatusText.Text = "تعذر تحميل الواجهة.";
                StatusText.Visibility = Visibility.Visible;
                WebView.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            if (WebView.CoreWebView2 is not null)
            {
                WebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                WebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
        }
        catch
        {
            // Closing must still continue.
        }

        _cts?.Cancel();
        var api = _api;
        _api = null;
        if (api is not null)
        {
            _ = StopApiAsync(api);
        }
    }

    private static async Task StopApiAsync(WebApplication api)
    {
        try
        {
            await api.StopAsync();
            await api.DisposeAsync();
        }
        catch
        {
            // Process exit will tear down the local server.
        }
    }

    private static bool IsLoopbackAppUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == "about")
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp && uri.Host == "127.0.0.1";
    }

    private static bool IsBlankUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return true;
        }

        return uri.Scheme == "about" || string.Equals(uri.AbsolutePath, "/blank", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
