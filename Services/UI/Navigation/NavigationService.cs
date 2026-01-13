using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.Navigation;
using NavigationIntegrationSystem.UI.Views.Pages;

namespace NavigationIntegrationSystem.Services.UI.Navigation;

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
            case NavKeys.Integration: m_Frame.Navigate(typeof(IntegrationPage)); break;
            case NavKeys.Devices: m_Frame.Navigate(typeof(DevicesPage)); break;
            case NavKeys.Logs: m_Frame.Navigate(typeof(LogsPage)); break;
        }
    }
    #endregion
}