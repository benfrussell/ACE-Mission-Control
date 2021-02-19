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
using Windows.ApplicationModel.Resources.Core;
using Windows.UI.Xaml.Controls;
using Windows.Globalization;
using Windows.UI.Xaml.Media.Animation;

namespace ACE_Mission_Control.ViewModels
{
    // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/pages/settings.md
    public class SettingsViewModel : ViewModelBase
    {
        public static event EventHandler<EventArgs> LanguageChangedEvent;

        private string _versionDescription;
        public string VersionDescription
        {
            get { return _versionDescription; }

            set { Set(ref _versionDescription, value); }
        }

        public int CurrentLanguageIndex { get; set; }

        private Dictionary<string, int> languageIndexes = new Dictionary<string, int>() { ["en-CA"] = 0, ["fr-CA"] = 1 };

        public SettingsViewModel()
        {
            
        }

        public async Task InitializeAsync()
        {
            CurrentLanguageIndex = languageIndexes[ApplicationLanguages.PrimaryLanguageOverride];
            RaisePropertyChanged("CurrentLanguageIndex");
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

        public RelayCommand<ComboBoxItem> LanguageSelectionCommand => new RelayCommand<ComboBoxItem>((args) => languageSelectionCommand(args));
        private void languageSelectionCommand(ComboBoxItem item)
        {
            ApplicationLanguages.PrimaryLanguageOverride = item.Content as string;
            CurrentLanguageIndex = languageIndexes[ApplicationLanguages.PrimaryLanguageOverride];
            RaisePropertyChanged("CurrentLanguageIndex");
            ResourceContext.GetForCurrentView().Reset();
            ResourceContext.GetForViewIndependentUse().Reset();
            ShellViewModel.NavigationService.Navigate(typeof(SettingsViewModel).FullName, new SuppressNavigationTransitionInfo());
            LanguageChangedEvent?.Invoke(this, new EventArgs());
        }
    }
}
