using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

using ACE_Mission_Control.Helpers;
using ACE_Mission_Control.Services;
using ACE_Mission_Control.Views;
using ACE_Mission_Control.Core.Models;

using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

using WinUI = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.Resources.Core;
using Windows.ApplicationModel.Core;
using System.ComponentModel;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.ApplicationModel.DataTransfer;
using GalaSoft.MvvmLight.Messaging;

namespace ACE_Mission_Control.ViewModels
{
    public class DronePageParams
    {
        public int DroneID;
        public int PivotItem;
        public bool ConnectionOpen;
        public bool PlannerOpen;
        public bool ControlsOpen;

        public DronePageParams(int droneID)
        {
            DroneID = droneID;
            PivotItem = 0;
            ConnectionOpen = true;
            PlannerOpen = false;
            ControlsOpen = false;
        }
    }

    public class ScrollAlertDataGridMessage : MessageBase { public AlertEntry newEntry { get; set; } }

    public class ShellViewModel : ViewModelBase
    {
        private List<object> _menuItems;
        public List<object> MenuItems
        {
            get { return _menuItems; }
            set
            {
                if (_menuItems == value)
                    return;
                _menuItems = value;
                RaisePropertyChanged("MenuItems");
            }
        }

        private string _ugcsConnectText;
        public string UGCSConnectText
        {
            get { return _ugcsConnectText; }
            set { Set(ref _ugcsConnectText, value); }
        }

        private bool _isUgCSRefreshEnabled;

        public bool IsUgCSRefreshEnabled
        {
            get { return _isUgCSRefreshEnabled; }
            set { Set(ref _isUgCSRefreshEnabled, value); }
        }

        public ObservableCollection<AlertEntry> AlertEntries
        {
            get { return Alerts.AlertLog; }
        }

        private GridLength alertRowHeight;
        public GridLength AlertRowHeight
        {
            get { return alertRowHeight; }
            set
            {
                if (alertRowHeight == value)
                    return;
                alertRowHeight = value;
                RaisePropertyChanged();
            }
        }

        // Generated Code

        private readonly KeyboardAccelerator _altLeftKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu);
        private readonly KeyboardAccelerator _backKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.GoBack);

        private bool _isBackEnabled;
        private IList<KeyboardAccelerator> _keyboardAccelerators;
        private WinUI.NavigationView _navigationView;
        private object _selected;
        private ICommand _loadedCommand;
        private ICommand _itemInvokedCommand;
        private Dictionary<int, DronePageParams> dronePageParams;

        public bool IsBackEnabled
        {
            get { return _isBackEnabled; }
            set { Set(ref _isBackEnabled, value); }
        }

        public static NavigationServiceEx NavigationService => ViewModelLocator.Current.NavigationService;

        public object Selected
        {
            get { return _selected; }
            set { Set(ref _selected, value); }
        }

        public ICommand LoadedCommand => _loadedCommand ?? (_loadedCommand = new RelayCommand(OnLoaded));

        public ICommand ItemInvokedCommand => _itemInvokedCommand ?? (_itemInvokedCommand = new RelayCommand<WinUI.NavigationViewItemInvokedEventArgs>(OnItemInvoked));

        public ShellViewModel()
        {
        }

        public void Initialize(Frame frame, WinUI.NavigationView navigationView, IList<KeyboardAccelerator> keyboardAccelerators)
        {
            // Generated initialization
            _navigationView = navigationView;
            _keyboardAccelerators = keyboardAccelerators;
            NavigationService.Frame = frame;
            NavigationService.NavigationFailed += Frame_NavigationFailed;
            NavigationService.Navigated += Frame_Navigated;
            _navigationView.BackRequested += OnBackRequested;

            DroneController.Drones.CollectionChanged += Drones_CollectionChanged;
            SettingsViewModel.LanguageChangedEvent += SettingsViewModel_LanguageChangedEvent;
            Alerts.AlertLog.CollectionChanged += AlertLog_CollectionChanged;

            IsUgCSRefreshEnabled = UGCSClient.IsConnected;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            UGCSConnectText = UGCSClient.ConnectionMessage;

            MenuItems = GetMenuItems().ToList();
            dronePageParams = new Dictionary<int, DronePageParams>();
            AlertRowHeight = new GridLength(0);
        }

        private void SettingsViewModel_LanguageChangedEvent(object sender, EventArgs e)
        {
            MenuItems = GetMenuItems().ToList();
        }

        private void Drones_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MenuItems = GetMenuItems().ToList();
            foreach (Drone d in e.NewItems)
            {
                d.PropertyChanged += Drone_PropertyChanged;
            }
        }

        private async void AlertLog_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RaisePropertyChanged("AlertEntries");
                var msg = new ScrollAlertDataGridMessage() { newEntry = Alerts.AlertLog[Alerts.AlertLog.Count - 1] };
                Messenger.Default.Send(msg);
            });
        }

        private async void UGCSClient_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "ConnectionMessage")
                    UGCSConnectText = UGCSClient.ConnectionMessage;
                else if (e.PropertyName == "IsConnected")
                    IsUgCSRefreshEnabled = UGCSClient.IsConnected;
            });
        }

        private void Drone_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name")
            {
                MenuItems = GetMenuItems().ToList();
            }
        }

        public IEnumerable<object> GetMenuItems()
        {
            yield return ViewModelLocator.Current.WelcomeViewModel; 

            foreach (Drone d in DroneController.Drones)
            {
                yield return d;
            }
        }

        private async void OnLoaded()
        {
            // Generated code
            // Keyboard accelerators are added here to avoid showing 'Alt + left' tooltip on the page.
            // More info on tracking issue https://github.com/Microsoft/microsoft-ui-xaml/issues/8
            _keyboardAccelerators.Add(_altLeftKeyboardAccelerator);
            _keyboardAccelerators.Add(_backKeyboardAccelerator);
            await Task.CompletedTask;
        }

        private void OnItemInvoked(WinUI.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavigationService.Navigate(typeof(SettingsViewModel).FullName, new SuppressNavigationTransitionInfo());
                return;
            }

            if (NavigationService.Frame.CurrentSourcePageType == typeof(MainPage))
                SaveDronePageParams();

            object itemTag = (args.InvokedItemContainer as WinUI.NavigationViewItem).Tag;
            if (itemTag.GetType() == typeof(int))
            {
                var pageParams = GetDronePageParams((int)itemTag);
                AlertRowHeight = new GridLength(1, GridUnitType.Star);

                // Suppress the transition if navigating from the same type of page
                if (NavigationService.Frame.CurrentSourcePageType == typeof(MainPage))
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", pageParams, new SuppressNavigationTransitionInfo());
                else
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", pageParams);
            }
            else
            {
                AlertRowHeight = new GridLength(0);
                NavigationService.Navigate(itemTag as string);
            }
        }

        private DronePageParams GetDronePageParams(int droneID)
        {
            if (!dronePageParams.ContainsKey(droneID))
                dronePageParams[droneID] = new DronePageParams(droneID);
            return dronePageParams[droneID];
        }

        private void SaveDronePageParams()
        {
            var mainVM = ViewModelLocator.Current.MainViewModel;
            var id = mainVM.AttachedDrone.ID;
            if (!dronePageParams.ContainsKey(id))
                return;

            dronePageParams[id].PivotItem = mainVM.SelectedIndex;
            var missionVM = ViewModelLocator.Current.MissionViewModel;
            dronePageParams[id].ConnectionOpen = missionVM.ConnectionExpanded;
            dronePageParams[id].PlannerOpen = missionVM.PlannerExpanded;
            dronePageParams[id].ControlsOpen = missionVM.ControlsExpanded;
        }

        public RelayCommand RefreshUGCSMissionsCommand => new RelayCommand(() => refreshUGCSMissionsCommand());
        private void refreshUGCSMissionsCommand()
        {
            UGCSClient.RequestMissions();
        }

        private void OnBackRequested(WinUI.NavigationView sender, WinUI.NavigationViewBackRequestedEventArgs args)
        {
            NavigationService.GoBack();
        }

        private void Frame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw e.Exception;
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            IsBackEnabled = NavigationService.CanGoBack;
            if (e.SourcePageType == typeof(SettingsPage))
            {
                Selected = _navigationView.SettingsItem as WinUI.NavigationViewItem;
            }
            else if (e.SourcePageType == typeof(WelcomePage))
            {
                Selected = _navigationView.MenuItems
                            .OfType<WinUI.NavigationViewItem>()
                            .FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));
            }
            else
            {
                Selected = _navigationView.MenuItems
                            .OfType<WinUI.NavigationViewItem>()
                            .FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));
            }
        }

        private bool IsMenuItemForPageType(WinUI.NavigationViewItem menuItem, Type sourcePageType)
        {
            var navigatedPageKey = NavigationService.GetNameOfRegisteredPage(sourcePageType);
            var pageKey = menuItem.GetValue(NavHelper.NavigateToProperty) as string;
            return pageKey == navigatedPageKey;
        }

        private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
        {
            var keyboardAccelerator = new KeyboardAccelerator() { Key = key };
            if (modifiers.HasValue)
            {
                keyboardAccelerator.Modifiers = modifiers.Value;
            }

            keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;
            return keyboardAccelerator;
        }

        private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var result = NavigationService.GoBack();
            args.Handled = result;
        }

        public RelayCommand<DataGrid> AlertCopyCommand => new RelayCommand<DataGrid>((grid) => alertCopyCommand(grid));
        private void alertCopyCommand(DataGrid grid)
        {
            string copiedText = "";
            AlertToString alertConverter = new AlertToString();
            foreach (object item in grid.SelectedItems)
            {
                var entry = (AlertEntry)item;
                copiedText += entry.Timestamp.ToLongTimeString() + " " + alertConverter.Convert(entry, typeof(string), null, null) + "\n";
            }
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(copiedText);
            Clipboard.SetContent(dataPackage);
        }

        public RelayCommand ContentSizeChangedCommand => new RelayCommand(() => contentSizeChanged());
        private void contentSizeChanged()
        {
            if (Alerts.AlertLog.Count > 0)
            {
                var msg = new ScrollAlertDataGridMessage() { newEntry = Alerts.AlertLog[Alerts.AlertLog.Count - 1] };
                Messenger.Default.Send(msg);
            }
        }
    }
}
