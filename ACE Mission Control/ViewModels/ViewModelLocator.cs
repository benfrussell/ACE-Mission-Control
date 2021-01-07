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
            Register<ConsoleViewModel, ConsolePage>();
            Register<WelcomeViewModel, WelcomePage>();
            Register<PlannerViewModel, PlannerPage>();
        }

        public SettingsViewModel SettingsViewModel => SimpleIoc.Default.GetInstance<SettingsViewModel>();

        public ShellViewModel ShellViewModel => SimpleIoc.Default.GetInstance<ShellViewModel>();

        public NavigationServiceEx NavigationService => SimpleIoc.Default.GetInstance<NavigationServiceEx>();

        public MainViewModel MainViewModel => SimpleIoc.Default.GetInstance<MainViewModel>();

        public MissionViewModel MissionViewModel => SimpleIoc.Default.GetInstance<MissionViewModel>();

        public ConfigViewModel ConfigViewModel => SimpleIoc.Default.GetInstance<ConfigViewModel>();

        public ConsoleViewModel ConsoleViewModel => SimpleIoc.Default.GetInstance<ConsoleViewModel>();

        public WelcomeViewModel WelcomeViewModel => SimpleIoc.Default.GetInstance<WelcomeViewModel>();

        public PlannerViewModel PlannerViewModel => SimpleIoc.Default.GetInstance<PlannerViewModel>();

        public void Register<VM, V>()
            where VM : class
        {
            SimpleIoc.Default.Register<VM>();

            NavigationService.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
