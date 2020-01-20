using System;

using GalaSoft.MvvmLight.Ioc;

using ACE_Mission_Control.Services;
using ACE_Mission_Control.Views;
using GalaSoft.MvvmLight;

namespace ACE_Mission_Control.ViewModels
{
    [Windows.UI.Xaml.Data.Bindable]
    public class ViewModelLocator
    {
        private static ViewModelLocator _current;

        public static ViewModelLocator Current => _current ?? (_current = new ViewModelLocator());

        private ViewModelLocator()
        {
            SimpleIoc.Default.Register(() => new NavigationServiceEx());
            SimpleIoc.Default.Register<ShellViewModel>();
            Register<MainViewModel, MainPage>();
            Register<SettingsViewModel, SettingsPage>();
            Register<MissionViewModel, MissionPage>();
            Register<ConfigViewModel, ConfigPage>();
        }

        public SettingsViewModel SettingsViewModel => SimpleIoc.Default.GetInstance<SettingsViewModel>();

        public ShellViewModel ShellViewModel => SimpleIoc.Default.GetInstance<ShellViewModel>();

        public NavigationServiceEx NavigationService => SimpleIoc.Default.GetInstance<NavigationServiceEx>();

        public void Register<VM, V>()
            where VM : class
        {
            SimpleIoc.Default.Register<VM>();

            NavigationService.Configure(typeof(VM).FullName, typeof(V));
        }
        public ViewModelBase GetViewModel<T>(int id) where T : ViewModelBase
        {
            var vm = SimpleIoc.Default.GetInstance<T>(id.ToString());
            if (vm.GetType().IsSubclassOf(typeof(DroneViewModelBase)))
            {
                (vm as DroneViewModelBase).SetDroneID(id);
            }
                
            return vm;
        }
    }
}
