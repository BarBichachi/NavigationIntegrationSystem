using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

namespace NavigationIntegrationSystem.UI.Views.Panes
{
    public sealed partial class DeviceInspectPaneView : UserControl
    {
        #region Properties
        public DeviceCardViewModel? ViewModel { get; set; }
        #endregion

        #region Constructors
        public DeviceInspectPaneView()
        {
            InitializeComponent();
        }
        #endregion
    }
}
