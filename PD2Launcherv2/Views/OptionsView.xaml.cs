using System.Windows.Controls;
using PD2Launcherv2.ViewModels;
using PD2Shared.Utils;

namespace PD2Launcherv2.Views
{
    /// <summary>
    /// Interaction logic for OptionsView.xaml
    /// </summary>
    public partial class OptionsView : Page
    {
        public OptionsView()
        {
            InitializeComponent();
            DataContext = App.Resolve<OptionsViewModel>();

            // It's much easier to have this set up here than via the model
            if (Wine.IsRunningUnderWine)
            {
                WindowsPermissionsText.Text = "Wine configuration";
                SetWindowsPermissions.Content = "Apply";
                SetWindowsPermissions.ToolTip = null;
                RemoveWindowsPermissions.Content = "Remove";
                RemoveWindowsPermissions.ToolTip = null;
            }
            else
            {
                WineLogo32Image.Visibility = System.Windows.Visibility.Collapsed;
                WineLogo32ImageCopy.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}