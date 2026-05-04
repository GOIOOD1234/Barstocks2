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
    // פונקציות Win32 הכרחיות
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

    public MainWindow()
    {
        InitializeComponent();
        
        this.Opened += (s, e) => {
            UpdateFontSize();
            // ב-Windows נתחיל בלי Click-Through כדי שהמשתמש יוכל לגרור מיד
            // אם אתה רוצה שזה יהיה שקוף להקלקות כברירת מחדל, תפעיל פה EnableClickThrough
        };

        this.PointerEntered += (s, e) => {
            DisableClickThrough();
            MainBorder.Background = new SolidColorBrush(Color.Parse("#CC000000"));
            SettingsButton.Opacity = 1;
            ExpandButton.Opacity = 1;
        };

        this.PointerExited += (s, e) => {
            EnableClickThrough();
            MainBorder.Background = new SolidColorBrush(Color.Parse("#01000000")); // צבע כמעט שקוף שמאפשר HitTest
            SettingsButton.Opacity = 0;
            ExpandButton.Opacity = 0;
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

    private void MainBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // גרירה עובדת רק כשהחלון אינו במצב Click-Through
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void EnableClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var handle = this.TryGetPlatformHandle()?.Handle;
        if (handle == null || handle == IntPtr.Zero) return;
        int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);
        SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
    }

    private void DisableClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var handle = this.TryGetPlatformHandle()?.Handle;
        if (handle == null || handle == IntPtr.Zero) return;
        int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);
        SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
    }

    // --- שאר הפונקציות (דיאלוגים, טעינת נתונים וכו') ---

    private async void ExpandButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await ShowBarSizeDialog(this, (int)this.Position.X, (int)this.Position.Y, (int)this.Width, (int)this.Height);
        if (result.success) {
            this.Position = new PixelPoint(result.x, result.y);
            this.Width = result.width;
            this.Height = result.height;
            if (_user != null) {
                _user.BarX = result.x; _user.BarY = result.y; _user.BarHeight = result.height;
                await _dataSystem.SaveUser(_user);
            }
            UpdateFontSize();
        }
    }

    public static async Task<(bool success, int x, int y, int width, int height)> ShowBarSizeDialog(Window parent, int cX, int cY, int cW, int cH)
    {
        var tcs = new TaskCompletionSource<(bool success, int x, int y, int width, int height)>();
        var tbX = new TextBox { Text = cX.ToString(), Margin = new Thickness(0,0,8,0) };
        var tbY = new TextBox { Text = cY.ToString() };
        var tbW = new TextBox { Text = cW.ToString(), Margin = new Thickness(0,0,8,0) };
        var tbH = new TextBox { Text = cH.ToString() };

        var btnFull = new Button { Content = "FULL SCREEN", Background = Brushes.Red, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0,10,0,0) };
        var btnReset = new Button { Content = "RESET TO DEFAULT", Background = Brushes.Gray, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0,5,0,0) };
        var btnSave = new Button { Content = "Apply", Width = 100, Background = Brushes.DarkGreen, Foreground = Brushes.White };
        var btnCancel = new Button { Content = "Cancel", Width = 100 };

        btnFull.Click += (s,e) => {
            var screen = parent.Screens.ScreenFromVisual(parent);
            if (screen != null) {
                tbX.Text = "0"; tbY.Text = "0";
                tbW.Text = ((int)(screen.Bounds.Width / screen.Scaling)).ToString();
                tbH.Text = ((int)(screen.Bounds.Height / screen.Scaling)).ToString();
            }
        };
        btnReset.Click += (s,e) => {
            var screen = parent.Screens.ScreenFromVisual(parent);
            if (screen != null) tbW.Text = ((int)(screen.WorkingArea.Width / screen.Scaling)).ToString();
            tbH.Text = "45";
        };

        var layout = new StackPanel { Margin = new Thickness(20), Children = {
            new TextBlock { Text = "Position (X, Y)" }, new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { tbX, tbY } },
            new TextBlock { Text = "Size (W, H)" }, new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { tbW, tbH } },
            btnFull, btnReset,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { btnCancel, btnSave }, Margin = new Thickness(0,20,0,0) }
        }};
        Grid.SetColumn(tbX, 0); Grid.SetColumn(tbY, 1); Grid.SetColumn(tbW, 0); Grid.SetColumn(tbH, 1);

        var win = new Window { Content = layout, SizeToContent = SizeToContent.WidthAndHeight, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        btnSave.Click += (s,e) => { tcs.TrySetResult((true, int.Parse(tbX.Text), int.Parse(tbY.Text), int.Parse(tbW.Text), int.Parse(tbH.Text))); win.Close(); };
        btnCancel.Click += (s,e) => { tcs.TrySetResult((false, 0,0,0,0)); win.Close(); };
        await win.ShowDialog(parent);
        return tcs.Task.IsCompleted ? await tcs.Task : (false, 0,0,0,0);
    }

    private void StartScrolling() {
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _scrollTimer.Tick += (s, e) => {
            _textX -= 1.5;
            if (TickerText.Bounds.Width > 0 && _textX < -TickerText.Bounds.Width) _textX = this.Width;
            Canvas.SetLeft(TickerText, _textX);
            Canvas.SetTop(TickerText, (this.Height - TickerText.Bounds.Height) / 2);
        };
        _scrollTimer.Start();
    }

    public async void LoadData() {
        _user = await _dataSystem.LoadUser() ?? new User { SymbolStocks = new List<string> { "AAPL", "TSLA" }, BarHeight = 45 };
        this.Position = new PixelPoint(_user.BarX, _user.BarY);
        this.Height = _user.BarHeight;
        this.Width = 1920;
        LoadProjects();
    }

    public async void LoadProjects() {
        if (_user?.SymbolStocks == null) return;
        try {
            var symbols = _user.SymbolStocks.ToArray();
            var query = await Yahoo.Symbols(symbols).Fields(Field.Symbol, Field.RegularMarketPrice, Field.RegularMarketChangePercent).QueryAsync();
            await Dispatcher.UIThread.InvokeAsync(() => {
                TickerText.Inlines?.Clear();
                foreach (var s in symbols) {
                    if (query.TryGetValue(s, out var data)) {
                        double change = Convert.ToDouble(data.RegularMarketChangePercent);
                        var run = new Run($"{s} {Convert.ToDecimal(data.RegularMarketPrice):N2}$ {(change >= 0 ? "▲" : "▼")} {change:F2}% ") { Foreground = change >= 0 ? Brushes.LimeGreen : Brushes.Red };
                        TickerText.Inlines?.Add(run);
                        TickerText.Inlines?.Add(new Run("      "));
                    }
                }
            });
        } catch { }
    }

    private async void SetFollowStocksButton_OnClick(object? sender, RoutedEventArgs e) {
        var result = await ShowInputContentDialog("Edit Stocks", this, string.Join(", ", _user!.SymbolStocks));
        if (result.success) {
            _user.SymbolStocks = result.content.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToUpper().Trim()).ToList();
            await _dataSystem.SaveUser(_user); LoadProjects();
        }
    }

    public static async Task<(bool success, string content)> ShowInputContentDialog(string title, Window parent, string initial) {
        var tcs = new TaskCompletionSource<(bool success, string content)>();
        var tb = new TextBox { Text = initial, Width = 300 };
        var btn = new Button { Content = "Save" };
        var win = new Window { Content = new StackPanel { Children = { new TextBlock { Text = title }, tb, btn }, Margin = new Thickness(20) }, SizeToContent = SizeToContent.WidthAndHeight };
        btn.Click += (s,e) => { tcs.TrySetResult((true, tb.Text ?? "")); win.Close(); };
        await win.ShowDialog(parent);
        return await tcs.Task;
    }
}
