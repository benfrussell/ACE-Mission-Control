using System;
using System.Threading.Tasks;
using System.Windows.Input;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using ACE_Mission_Control.Helpers;
using ACE_Mission_Control.Services;
using ACE_Mission_Control.Core.Models;

using Windows.ApplicationModel;
using Windows.UI.Xaml;
using System.Collections.Generic;

namespace ACE_Mission_Control.ViewModels
{
    // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/pages/settings.md
    public class SettingsViewModel : ViewModelBase
    {
        private string _sshKeyGenerationText;
        public string SSHKeyGenerationText
        {
            set
            {
                _sshKeyGenerationText = value;
            }
            get
            {
                return _sshKeyGenerationText;
            }
        }

        private List<string> _numDronesItems;
        public List<string> NumDronesItems
        {
            get { return _numDronesItems; }
        }

        private string _numDronesValue;
        public string NumDronesValue
        {
            get { return _numDronesValue; }
            set
            {
                if (value == _numDronesValue)
                    return;
                _numDronesValue = value;
                DroneController.SetDroneNum(int.Parse(value));
                RaisePropertyChanged("NumDronesValue");
            }
        }

        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }

            set { Set(ref _elementTheme, value); }
        }

        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }

            set { Set(ref _versionDescription, value); }
        }

        private ICommand _switchThemeCommand;

        public ICommand SwitchThemeCommand
        {
            get
            {
                if (_switchThemeCommand == null)
                {
                    _switchThemeCommand = new RelayCommand<ElementTheme>(
                        async (param) =>
                        {
                            ElementTheme = param;
                            await ThemeSelectorService.SetThemeAsync(param);
                        });
                }

                return _switchThemeCommand;
            }
        }

        public SettingsViewModel()
        {
            _numDronesItems = new List<string>();
            for (int i = 1; i <= DroneController.MAX_DRONES; i++)
            {
                _numDronesItems.Add(i.ToString());
            }

            NumDronesValue = NumDronesItems[0];

            SSHKeyGenerationText = "Last key generated whenever.";
        }

        public void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            /*OnboardComputerClient obc_client = new OnboardComputerClient();
            obc_client.GenerateSSHKeyFiles("pass");*/
        }

        public async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            await Task.CompletedTask;
        }

        private string GetVersionDescription()
        {
            var appName = "AppDisplayName".GetLocalized();
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{appName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
