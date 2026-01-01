using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.Navigation;
using NavigationIntegrationSystem.UI.Pages;

namespace NavigationIntegrationSystem.Services.UI;

// Centralizes page navigation so UI logic stays out of code-behind
public sealed class NavigationService
{
    #region Private Fields
    private Frame? m_Frame;
    #endregion

    #region Public Methods
    // Attaches the frame used for navigation
    public void Attach(Frame i_Frame) { m_Frame = i_Frame; }

    // Navigates to a page by key
    public void Navigate(string i_Key)
    {
        if (m_Frame == null) { return; }

        switch (i_Key)
        {
            case NavKeys.Dashboard: m_Frame.Navigate(typeof(DashboardPage)); break;
            case NavKeys.Devices: m_Frame.Navigate(typeof(DevicesPage)); break;
            case NavKeys.Logs: m_Frame.Navigate(typeof(LogsPage)); break;
        }
    }
    #endregion
}