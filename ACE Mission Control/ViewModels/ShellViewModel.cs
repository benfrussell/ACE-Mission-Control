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

namespace ACE_Mission_Control.ViewModels
{
    public class ShellViewModel : ViewModelBase
    {
        private string _header;
        public string Header
        {
            get { return _header; }
            set
            {
                if (_header == value)
                    return;
                _header = value;
                RaisePropertyChanged("Header");
            }
        }

        private List<WinUI.NavigationViewItemBase> _menuItems;
        public List<WinUI.NavigationViewItemBase> MenuItems
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

        // Generated Code

        private readonly KeyboardAccelerator _altLeftKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu);
        private readonly KeyboardAccelerator _backKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.GoBack);

        private bool _isBackEnabled;
        private IList<KeyboardAccelerator> _keyboardAccelerators;
        private WinUI.NavigationView _navigationView;
        private WinUI.NavigationViewItem _selected;
        private ICommand _loadedCommand;
        private ICommand _itemInvokedCommand;

        public bool IsBackEnabled
        {
            get { return _isBackEnabled; }
            set { Set(ref _isBackEnabled, value); }
        }

        public static NavigationServiceEx NavigationService => ViewModelLocator.Current.NavigationService;

        public WinUI.NavigationViewItem Selected
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
            MenuItems = GetMenuItems().ToList();
            DroneController.Drones.CollectionChanged += Drones_CollectionChanged;
        }

        private void Drones_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            MenuItems = GetMenuItems().ToList();
            foreach (Drone d in e.NewItems)
            {
                d.PropertyChanged += Drone_PropertyChanged;
            }
        }

        private void Drone_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name")
            {
                MenuItems = GetMenuItems().ToList();    
                if (NavigationService.Frame.Content is MainPage)
                {
                    Drone drone = sender as Drone;
                    if ((NavigationService.Frame.Content as MainPage).DroneID == drone.ID)
                        Header = drone.Name;
                }
            }
        }

        public IEnumerable<WinUI.NavigationViewItemBase> GetMenuItems()
        {
            yield return new WinUI.NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon(Symbol.Home),
                Tag = typeof(WelcomeViewModel).FullName
            };

            foreach (Drone d in DroneController.Drones)
            {
                yield return new WinUI.NavigationViewItem()
                {
                    Content = d.Name,
                    Icon = new SymbolIcon(Symbol.Send),
                    Tag = d
                };
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

            var item = args.InvokedItemContainer as WinUI.NavigationViewItem;

            if (item.Tag is Drone)
            {
                // Suppress the transition if navigating from the same type of page
                if (NavigationService.Frame.CurrentSourcePageType == typeof(MainPage))
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", (item.Tag as Drone).ID, new SuppressNavigationTransitionInfo());
                else
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", (item.Tag as Drone).ID);
            }
            else
            {
                NavigationService.Navigate(item.Tag as string);
            }
        }
        public RelayCommand RefreshDroneListCommand => new RelayCommand(() => refreshDroneListCommand());
        private void refreshDroneListCommand()
        {
            DroneController.LoadUGCSDrones();
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
                Header = "Settings";
            }
            else if (e.SourcePageType == typeof(WelcomePage))
            {
                Selected = _navigationView.MenuItems
                            .OfType<WinUI.NavigationViewItem>()
                            .FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));
                Header = " ";
            }
            else
            {
                Selected = _navigationView.MenuItems
                            .OfType<WinUI.NavigationViewItem>()
                            .FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));

                if (e.SourcePageType == typeof(MainPage))
                {
                    if (e.Parameter.GetType() == typeof(int))
                    {
                        foreach (Drone d in DroneController.Drones)
                        {
                            if (d.ID == (int)e.Parameter)
                            {
                                Header = (d as Drone).Name;
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (DroneController.Drones.Count > 0)
                        {
                            Header = DroneController.Drones[0].Name;
                        }
                    }
                }
                else
                {
                    Header = Selected.Content as string;
                }
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
    }
}
