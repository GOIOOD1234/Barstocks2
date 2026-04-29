using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Barstocks.Models;
using Barstocks.System;
using Avalonia.Controls.Documents;
using YahooFinanceApi;

namespace Barstocks.Views;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private User? _user;
    private DataSystem _dataSystem = new DataSystem();

    private double _textX;
    private DispatcherTimer? _scrollTimer;
    private DispatcherTimer? _refreshTimer;

    private double _normalWidth = 1920;

    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = this;

        _textX = _normalWidth;

        this.Opened += OnWindowOpened;
        this.PointerEntered += OnPointerEntered;
        this.PointerExited += OnPointerExited;
        this.PositionChanged += OnWindowPositionChanged;

        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == Window.HeightProperty) UpdateFontSize();
        };

        StartScrolling();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += (s, e) => LoadProjects();
        _refreshTimer.Start();

        LoadData();
    }

    private void UpdateFontSize()
    {
        double newSize = this.Height * 0.6;
        if (newSize > 5) TickerText.FontSize = newSize;
    }


    private void OnWindowOpened(object? sender, EventArgs e)
    {
        EnableClickThrough();
        SettingsButton.Opacity = 0;
        ExpandButton.Opacity = 0;
        UpdateFontSize();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        DisableClickThrough();
        SettingsButton.Opacity = 1;
        ExpandButton.Opacity = 1;
        this.PointerPressed += BeginDrag;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        EnableClickThrough();
        SettingsButton.Opacity = 0;
        ExpandButton.Opacity = 0;
        this.PointerPressed -= BeginDrag;
    }

    private void BeginDrag(object? sender, PointerPressedEventArgs e) => this.BeginMoveDrag(e);

    private async void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_user != null)
        {
            _user.BarX = e.Point.X;
            _user.BarY = e.Point.Y;
            await _dataSystem.SaveUser(_user);
        }

        CheckScreenSize(e.Point);
    }

    private void CheckScreenSize(PixelPoint pos)
    {
        var screens = this.Screens;
        if (screens == null) return;
        var centerX = pos.X + (int)(this.Width / 2);
        var centerY = pos.Y + (int)(this.Height / 2);

        var currentScreen = screens.All.FirstOrDefault(s =>
            centerX >= s.WorkingArea.X && centerX <= s.WorkingArea.X + s.WorkingArea.Width &&
            centerY >= s.WorkingArea.Y && centerY <= s.WorkingArea.Y + s.WorkingArea.Height);

        if (currentScreen != null)
        {
            double screenWidth = currentScreen.WorkingArea.Width / currentScreen.Scaling;
            if (Math.Abs(this.Width - screenWidth) > 1)
            {
                _normalWidth = screenWidth;
                this.Width = _normalWidth;
                _textX = _normalWidth;
            }
        }
    }


    private void EnableClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle == null || handle == IntPtr.Zero) return;
        int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);
        SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    private void DisableClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle == null || handle == IntPtr.Zero) return;
        int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);
        SetWindowLong(handle.Value, GWL_EXSTYLE, (exStyle | WS_EX_LAYERED) & ~WS_EX_TRANSPARENT);
    }


    private async void ExpandButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await ShowBarSizeDialog(this, (int)this.Position.X, (int)this.Position.Y, (int)this.Width,
            (int)this.Height);
        if (!result.success) return;

        var screen = this.Screens.ScreenFromVisual(this);
        if (screen != null && result.width >= (screen.Bounds.Width / screen.Scaling) - 5)
        {
            this.WindowState = WindowState.FullScreen;
        }
        else
        {
            this.WindowState = WindowState.Normal;
            this.Position = new PixelPoint(result.x, result.y);
            this.Width = result.width;
            this.Height = result.height;
        }

        _normalWidth = result.width;

        if (_user != null)
        {
            _user.BarX = result.x;
            _user.BarY = result.y;
            _user.BarHeight = result.height;
            await _dataSystem.SaveUser(_user);
        }

        UpdateFontSize();
    }

    public static async Task<(bool success, int x, int y, int width, int height)> ShowBarSizeDialog(Window parent,
        int cX, int cY, int cW, int cH)
    {
        var tcs = new TaskCompletionSource<(bool success, int x, int y, int width, int height)>();

        var tbX = new TextBox { Text = cX.ToString(), Margin = new Thickness(0, 0, 8, 0) };
        var tbY = new TextBox { Text = cY.ToString() };
        var tbW = new TextBox { Text = cW.ToString(), Margin = new Thickness(0, 0, 8, 0) };
        var tbH = new TextBox { Text = cH.ToString() };

        var btnSave = new Button
        {
            Content = "Apply", Background = Brushes.DarkGreen, Foreground = Brushes.White, Width = 100,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        var btnCancel = new Button
            { Content = "Cancel", Width = 100, HorizontalContentAlignment = HorizontalAlignment.Center };

        var btnAbsoluteFull = new Button
        {
            Content = "FULL SCREEN",
            Background = Brushes.Red,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        btnAbsoluteFull.Click += (s, e) =>
        {
            var screen = parent.Screens.ScreenFromVisual(parent);
            if (screen != null)
            {
                double scale = screen.Scaling;
                tbX.Text = "0";
                tbY.Text = "0";
                tbW.Text = ((int)(screen.Bounds.Width / scale)).ToString();
                tbH.Text = ((int)(screen.Bounds.Height / scale)).ToString();
            }
        };

        var layout = new StackPanel
        {
            Margin = new Thickness(20), Width = 320, Children =
            {
                new TextBlock { Text = "Position (X, Y)", FontWeight = FontWeight.Bold },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { tbX, tbY } },
                new TextBlock
                    { Text = "Size (W, H)", Margin = new Thickness(0, 10, 0, 0), FontWeight = FontWeight.Bold },
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { tbW, tbH } },
                btnAbsoluteFull,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { btnCancel, btnSave },
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                }
            }
        };

        Grid.SetColumn(tbX, 0);
        Grid.SetColumn(tbY, 1);
        Grid.SetColumn(tbW, 0);
        Grid.SetColumn(tbH, 1);

        var win = new Window
        {
            Content = layout,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = "Full Screen Config"
        };

        btnSave.Click += (s, e) =>
        {
            if (int.TryParse(tbX.Text, out int x) && int.TryParse(tbY.Text, out int y) &&
                int.TryParse(tbW.Text, out int w) && int.TryParse(tbH.Text, out int h))
                tcs.TrySetResult((true, x, y, w, h));
            win.Close();
        };

        btnCancel.Click += (s, e) =>
        {
            tcs.TrySetResult((false, 0, 0, 0, 0));
            win.Close();
        };

        await win.ShowDialog(parent);
        return tcs.Task.IsCompleted ? await tcs.Task : (false, 0, 0, 0, 0);
    }


    private void StartScrolling()
    {
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _scrollTimer.Tick += (s, e) =>
        {
            _textX -= 1.5;
            if (TickerText.Bounds.Width > 0 && _textX < -TickerText.Bounds.Width)
                _textX = this.Width;

            Canvas.SetLeft(TickerText, _textX);
            Canvas.SetTop(TickerText, (this.Height - TickerText.Bounds.Height) / 2);
        };
        _scrollTimer.Start();
    }


    public async void LoadData()
    {
        User? getUser = await _dataSystem.LoadUser();
        _user = getUser ?? new User
        {
            name = "user", Id = Guid.NewGuid().ToString(), SymbolStocks = new List<string> { "AAPL", "TSLA" }, BarX = 0,
            BarY = 0, BarHeight = 45
        };
        if (getUser == null) await _dataSystem.SaveUser(_user);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.Position = new PixelPoint(_user.BarX, _user.BarY);
            this.Height = _user.BarHeight;
            this.Width = _normalWidth;
            _textX = this.Width;
            UpdateFontSize();
        });
        LoadProjects();
    }

    public async void LoadProjects()
    {
        if (_user?.SymbolStocks == null) return;
        try
        {
            var symbols = _user.SymbolStocks.ToArray();
            var query = await Yahoo.Symbols(symbols)
                .Fields(Field.Symbol, Field.RegularMarketPrice, Field.RegularMarketChangePercent).QueryAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TickerText.Inlines?.Clear();

                foreach (var s in symbols)
                {
                    if (query.TryGetValue(s, out var data))
                    {
                        double change = Convert.ToDouble(data.RegularMarketChangePercent);
                        string arrow = change >= 0 ? "▲" : "▼";
                        string sign = change >= 0 ? "+" : "";
                        string stockString =
                            $"{s}  {Convert.ToDecimal(data.RegularMarketPrice):N2}$  {arrow} {sign}{change:F2}%";

                        var run = new Run(stockString)
                        {
                            Foreground = change >= 0 ? Brushes.LimeGreen : Brushes.Red
                        };

                        TickerText.Inlines?.Add(run);

                        TickerText.Inlines?.Add(new Run("          ") { Foreground = Brushes.White });
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Yahoo Error: " + ex.Message);
        }
    }

    private async void SetFollowStocksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        var result = await ShowInputContentDialog("Edit Stocks", this, string.Join(", ", _user.SymbolStocks));
        if (result.success)
        {
            _user.SymbolStocks = result.content.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.ToUpper().Trim()).Distinct().ToList();
            await _dataSystem.SaveUser(_user);
            LoadProjects();
        }
    }

    public static async Task<(bool success, string content)> ShowInputContentDialog(string title, Window parent,
        string initial)
    {
        var tcs = new TaskCompletionSource<(bool success, string content)>();
        var tb = new TextBox { Text = initial, Width = 300 };
        var btn = new Button { Content = "Save" };
        var win = new Window
        {
            Content = new StackPanel
                { Children = { new TextBlock { Text = title }, tb, btn }, Margin = new Thickness(20) },
            SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        btn.Click += (s, e) =>
        {
            tcs.TrySetResult((true, tb.Text ?? ""));
            win.Close();
        };
        await win.ShowDialog(parent);
        return tcs.Task.IsCompleted ? await tcs.Task : (false, "");
    }
}
