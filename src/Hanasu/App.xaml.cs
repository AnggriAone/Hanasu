﻿using Crystal.Core;
using Crystal.Localization;
using Crystal.Messaging;
using Crystal.Navigation;
using Crystal.Services;
using Hanasu.Extensions;
using Hanasu.Model;
using Hanasu.SystemControllers;
using Hanasu.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace Hanasu
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : BaseCrystalApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void PreStartup()
        {
            EnableSelfAssemblyResolution = true;
            EnableCrystalLocalization = true;

            try
            {
                ServiceManager.RegisterService<Hanasu.Services.MessageBoxService>();
            }
            catch (Exception)
            {
            }

            NetworkCostController.Initialize();

            base.PreStartup();
        }


        internal void LoadStations()
        {
            if (AvailableStations == null)
                AvailableStations = new ObservableCollection<Station>();
            else
                if (AvailableStations.Count > 0)
                    AvailableStations.Clear();

            if (NetworkCostController.IsConnectedToInternet)
            {
                XDocument doc = XDocument.Load("https://raw.github.com/Amrykid/Hanasu/master/Stations.xml");
                var stationsElement = doc.Element("Stations");

                var stations = from x in stationsElement.Elements("Station")
                               where x.ContainsElement("StationType") ? x.Element("StationType").Value != "TV" : true
                               select new Station()
                               {
                                   Title = x.Element("Name").Value,
                                   StreamUrl = x.Element("DataSource").Value,
                                   PreprocessorFormat = x.ContainsElement("ExplicitExtension") ? x.Element("ExplicitExtension").Value : string.Empty,
                                   ImageUrl = x.ContainsElement("Logo") ? x.Element("Logo").Value : null,
                                   UnlocalizedFormat = x.Element("Format").Value,
                                   Format = LocalizationManager.GetLocalizedValue("Group" + x.Element("Format").Value),
                                   Subtitle = LocalizationManager.GetLocalizedValue("StationSubtitle"),
                                   ServerType = x.ContainsElement("ServerType") ? x.Element("ServerType").Value : "Raw"
                               };

                foreach (var x in stations)
                    AvailableStations.Add(x);
            }
            else
            {
                Crystal.Services.ServiceManager.Resolve<Crystal.Services.IMessageBoxService>()
                    .ShowMessage(
                        LocalizationManager.GetLocalizedValue("InternetConnectionHeader"),
                        LocalizationManager.GetLocalizedValue("NoInternetConnectionMsg"));
            }

            //          <Stations>
            //<Station>
            //  <Name>AnimeNfo</Name>
            //  <DataSource>http://www.animenfo.com/radio/listen.m3u</DataSource>
            //  <Homepage>http://www.animenfo.com/</Homepage>
            //  <Format>Anime</Format>
            //  <City>Tokyo, Japan</City>
            //  <Language>English</Language>
            //  <Cacheable>True</Cacheable>
            //  <ExplicitExtension>.m3u</ExplicitExtension>
            //  <Schedule type="page">http://www.animenfo.com/radio/schedule.php</Schedule>
            //  <Logo>http://d1i6vahw24eb07.cloudfront.net/s54119q.png</Logo>
            //  <ServerType>Shoutcast</ServerType>
            //</Station>

        }

        public ObservableCollection<Station> AvailableStations { get; set; }


        protected override void OnSearchActivated(SearchActivatedEventArgs args)
        {
            base.OnSearchActivated(args); //required

            RootFrame.Style = Resources["RootFrameStyle"] as Style; // Fixes background audio issue across pages http://social.msdn.microsoft.com/Forums/en-US/winappswithcsharp/thread/241ba3b4-3e2a-4f9b-a704-87c7b1be7988/

            LoadStations();

            NavigationService.NavigateTo<SearchPageViewModel>(new KeyValuePair<string, string>("query", args.QueryText));

            // Ensure the current window is active
            Window.Current.Activate();

        }


        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        //protected override void OnLaunched(LaunchActivatedEventArgs args)
        protected override void PostStartup(LaunchActivatedEventArgs args)
        {
            base.PostStartup(args);

            RootFrame.Style = Resources["RootFrameStyle"] as Style; // Fixes background audio issue across pages http://social.msdn.microsoft.com/Forums/en-US/winappswithcsharp/thread/241ba3b4-3e2a-4f9b-a704-87c7b1be7988/

            Frame rootFrame = RootFrame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            NavigationService.NavigateTo<MainPageViewModel>(new KeyValuePair<string, string>("args", args.Arguments));

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        public Windows.Media.PlayTo.PlayToManager ptm = null;

        public MediaElement MediaElement
        {
            get
            {
                DependencyObject rootGrid = VisualTreeHelper.GetChild(Window.Current.Content, 0);

                return (MediaElement)VisualTreeHelper.GetChild(rootGrid, 0);
            }
        }

    }
}
