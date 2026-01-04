using System;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Services.UI.Navigation;
using NavigationIntegrationSystem.UI.Navigation;
using NavigationIntegrationSystem.UI.ViewModels;

using Windows.Graphics;

using WinRT.Interop;

namespace NavigationIntegrationSystem;

// Hosts the main navigation shell and routes between pages
public sealed partial class MainWindow : Window
{
    #region Private Fields
    private readonly NavigationService m_NavigationService;
    private readonly LogService m_LogService;
    private const int c_DefaultWindowWidth = 1300;
    private const int c_DefaultWindowHeight = 820;
    #endregion

    #region Ctors
    public MainWindow(MainViewModel i_ViewModel, NavigationService i_NavigationService, LogService i_LogService)
    {
        InitializeComponent();
        RootGrid.DataContext = i_ViewModel;

        m_LogService = i_LogService;
        m_NavigationService = i_NavigationService;

        InitWindowLayout();
        InitLogging();
        InitNavigation();
    }
    #endregion

    #region Private Functions
    // Initializes the window logging behavior
    private void InitLogging()
    {
        m_LogService.Info(nameof(MainWindow), "MainWindow created");
    }

    // Initializes the navigation shell and routes to the default page
    private void InitNavigation()
    {
        string initialPage = NavKeys.Dashboard;

        m_NavigationService.Attach(ContentFrame);
        m_NavigationService.Navigate(initialPage);

        SelectNavigationItemByTag(initialPage);
    }

    // Selects a NavigationView item by its Tag value
    private void SelectNavigationItemByTag(string i_Tag)
    {
        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag as string == i_Tag)
            {
                Nav.SelectedItem = navItem;
                break;
            }
        }
    }

    // Initializes the window size and centers it on screen
    private void InitWindowLayout()
    {
        AppWindow appWindow = GetAppWindow();
        appWindow.Resize(new SizeInt32(c_DefaultWindowWidth, c_DefaultWindowHeight));
        CenterWindow(appWindow);
    }

    // Gets the current window AppWindow
    private AppWindow GetAppWindow()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    // Centers the window on the current display
    private void CenterWindow(AppWindow i_AppWindow)
    {
        DisplayArea displayArea = DisplayArea.GetFromWindowId(i_AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;

        int x = workArea.X + (workArea.Width - i_AppWindow.Size.Width) / 2;
        int y = workArea.Y + (workArea.Height - i_AppWindow.Size.Height) / 2;

        i_AppWindow.Move(new PointInt32(x, y));
    }
    #endregion

    #region Event Handlers
    // Navigates based on the selected menu item
    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag) { m_NavigationService.Navigate(tag); }
    }
    #endregion
}
