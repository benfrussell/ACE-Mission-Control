﻿using System;
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

namespace ACE_Mission_Control.ViewModels
{
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

        // Generated Code

        private readonly KeyboardAccelerator _altLeftKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu);
        private readonly KeyboardAccelerator _backKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.GoBack);

        private bool _isBackEnabled;
        private IList<KeyboardAccelerator> _keyboardAccelerators;
        private WinUI.NavigationView _navigationView;
        private object _selected;
        private ICommand _loadedCommand;
        private ICommand _itemInvokedCommand;

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

            IsUgCSRefreshEnabled = UGCSClient.IsConnected;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            UGCSConnectText = UGCSClient.ConnectionMessage;

            MenuItems = GetMenuItems().ToList();
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

            yield return ViewModelLocator.Current.FlightTimeViewModel;
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

            object itemTag = (args.InvokedItemContainer as WinUI.NavigationViewItem).Tag;
            if (itemTag.GetType() == typeof(int))
            {
                // Suppress the transition if navigating from the same type of page
                if (NavigationService.Frame.CurrentSourcePageType == typeof(MainPage))
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", (int)itemTag, new SuppressNavigationTransitionInfo());
                else
                    NavigationService.Navigate("ACE_Mission_Control.ViewModels.MainViewModel", (int)itemTag);
            }
            else
            {
                NavigationService.Navigate(itemTag as string);
            }
        }
        public RelayCommand RefreshUGCSMissionsCommand => new RelayCommand(() => refreshUGCSMissionsCommand());
        private void refreshUGCSMissionsCommand()
        {
            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                UGCSClient.RequestMissionsByID(1000);
            else
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
    }
}
