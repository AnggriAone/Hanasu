﻿using Crystal.Binding;
using Crystal.Localization;
using Crystal.Navigation;
using Hanasu.Model;
using Hanasu.SystemControllers;
using Hanasu.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Search;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Hanasu
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [Crystal.Navigation.NavigationSetViewModel(typeof(MainPageViewModel))]
    public sealed partial class MainPage : LayoutAwarePage
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += MainPage_Loaded;

            //Task.Run(() => Dispatcher.ProcessEvents(Windows.UI.Core.CoreProcessEventsOption.ProcessUntilQuit));

            CoreWindow.GetForCurrentThread().KeyDown += pageRoot_KeyDown_1; //http://stackoverflow.com/questions/11812059/windows-8-metro-focus-on-grid

        }
        MediaElement globalMediaElement = null;
        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            DependencyObject rootGrid = VisualTreeHelper.GetChild(Window.Current.Content, 0);
            globalMediaElement = (MediaElement)VisualTreeHelper.GetChild(rootGrid, 0);

            ((MainPageViewModel)this.DataContext).SetMediaElement(ref globalMediaElement);

            DetectMediaElementState();

            PlayToController.PlayToConnectionStateChanged += PlayToController_PlayToConnectionStateChanged;

            globalMediaElement.CurrentStateChanged += globalMediaElement_CurrentStateChanged;
            nowPlayingPanel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            AutoUpdatePropertyHelper.BindObjects<MainPageViewModel>(((MainPageViewModel)this.DataContext), x => x.CurrentStationName, stationTitle, TextBlock.TextProperty);

            AutoUpdatePropertyHelper.BindObjects<MainPageViewModel>(((MainPageViewModel)this.DataContext), x => x.CurrentStationSongData, songTitle, TextBlock.TextProperty);
        }

        void PlayToController_PlayToConnectionStateChanged()
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    if (PlayToController.IsConnectedViaPlayTo)
                    {
                        PlayToPanel.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        var img = new BitmapImage();
                        img.SetSource(PlayToController.CurrentConnectionDetails.Icon);

                        PlayToDeviceIcon.Source = img;

                        PlayToDeviceName.Text = PlayToController.CurrentConnectionDetails.FriendlyName;

                    }
                    else
                    {
                        PlayToPanel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        PlayToDeviceIcon.Source = null;

                        PlayToDeviceName.Text = string.Empty;
                    }
                });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            globalMediaElement.CurrentStateChanged -= globalMediaElement_CurrentStateChanged;
            this.Loaded -= MainPage_Loaded;
            PlayToController.PlayToConnectionStateChanged -= PlayToController_PlayToConnectionStateChanged;


            base.OnNavigatedFrom(e);
        }

        void globalMediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            DetectMediaElementState();
        }

        private void DetectMediaElementState()
        {
            if (globalMediaElement.CurrentState == MediaElementState.Playing)
                nowPlayingPanel.Visibility = Windows.UI.Xaml.Visibility.Visible;
            else if (globalMediaElement.CurrentState == MediaElementState.Closed || globalMediaElement.CurrentState == MediaElementState.Stopped)
                nowPlayingPanel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        public override void OnVisualStateChange(string newVisualState)
        {
            //layout aware page doesn't wanna work correctly... or its having trouble finding the views so im doing it manually

            switch (newVisualState)
            {
                case "Filled":
                case "FullScreenLandscape":
                case "FullScreenPortrait":
                    itemGridView.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    itemListView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    nowPlayingPanel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    backButton.Visibility = Windows.UI.Xaml.Visibility.Visible;

                    itemListView.Margin = new Thickness(0, 0, 0, 0);

                    foreach (Button ui in MediaControlPanel.Children)
                        ui.Margin = new Thickness(0, 0, 0, 0);
                    break;

                case "Snapped":
                    itemGridView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    itemListView.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    nowPlayingPanel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    backButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                    itemListView.Margin = new Thickness(0, 0, 0, 20);

                    foreach (Button ui in MediaControlPanel.Children)
                        ui.Margin = new Thickness(-15, 0, -15, 0);
                    break;
            }
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        private void Header_Click(object sender, RoutedEventArgs e)
        {
            var name = ((StationGroup)((Button)e.OriginalSource).DataContext).UnlocalizedName;

            NavigationService.NavigateTo<GroupPageViewModel>(new KeyValuePair<string, string>("groupName", name));
        }

        private async void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {

            var vm = ((MainPageViewModel)this.DataContext);
            var stat = (Station)e.ClickedItem;

            await Task.Run(() => Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () => vm.PlayStation(stat, globalMediaElement)));
        }

        private SearchPane searchPane = null;
        private void pageRoot_KeyDown_1(object sender, Windows.UI.Core‌.KeyEventArgs e)
        {
            int keyCode = (int)e.VirtualKey;
            if (keyCode == 0
                || (keyCode > 0 && keyCode < 32)
                || (keyCode > 33 && keyCode < 47)
                || (keyCode > 91 && keyCode < 165)
                || keyCode == 91 || keyCode > 166)
                return;


            string initialchar = Enum.GetName(typeof(Windows.System.VirtualKey), e.VirtualKey);


            if (searchPane == null)
            {
                searchPane = SearchPane.GetForCurrentView();

                searchPane.PlaceholderText = LocalizationManager.GetLocalizedValue("SearchPanePlaceholder"); //Needs to be localized.
                searchPane.ResultSuggestionChosen += searchPane_ResultSuggestionChosen;
                searchPane.QuerySubmitted += searchPane_QuerySubmitted;
                searchPane.SuggestionsRequested += searchPane_SuggestionsRequested;
                searchPane.VisibilityChanged += searchPane_VisibilityChanged;
            }

            if (!searchPane.Visible)
                searchPane.Show(initialchar);
        }

        void searchPane_ResultSuggestionChosen(SearchPane sender, SearchPaneResultSuggestionChosenEventArgs args)
        {
            foreach (StationGroup sg in ((MainPageViewModel)this.DataContext).AvailableStations)
                foreach (Station st in sg.Items)
                    if (st.Title.StartsWith(args.Tag, StringComparison.CurrentCultureIgnoreCase) || st.Title.Contains(args.Tag))
                    {
                        ((MainPageViewModel)this.DataContext).PlayStation(st, globalMediaElement);

                        break;
                    }
        }

        void searchPane_QuerySubmitted(SearchPane sender, SearchPaneQuerySubmittedEventArgs args)
        {
            foreach (StationGroup sg in ((MainPageViewModel)this.DataContext).AvailableStations)
                foreach (Station st in sg.Items)
                    if (st.Title.StartsWith(args.QueryText, StringComparison.CurrentCultureIgnoreCase) || st.Title.Contains(args.QueryText))
                    {
                        ((MainPageViewModel)this.DataContext).PlayStation(st, globalMediaElement);

                        break;
                    }
        }

        void searchPane_VisibilityChanged(SearchPane sender, SearchPaneVisibilityChangedEventArgs args)
        {
            if (args.Visible == false)
            {
                searchPane.SuggestionsRequested -= searchPane_SuggestionsRequested;
                searchPane.VisibilityChanged -= searchPane_VisibilityChanged;
                searchPane.ResultSuggestionChosen -= searchPane_ResultSuggestionChosen;
                searchPane.QuerySubmitted -= searchPane_QuerySubmitted;

                searchPane = null;


                this.Focus(Windows.UI.Xaml.FocusState.Programmatic);
                Window.Current.Activate();
            }
        }

        void searchPane_SuggestionsRequested(SearchPane sender, SearchPaneSuggestionsRequestedEventArgs args)
        {
            foreach (StationGroup sg in ((MainPageViewModel)this.DataContext).AvailableStations)
                foreach (Station st in sg.Items)
                    if (st.Title.StartsWith(args.QueryText, StringComparison.CurrentCultureIgnoreCase) || st.Title.Contains(args.QueryText))
                        args.Request.SearchSuggestionCollection.AppendQuerySuggestion(st.Title);
        }
    }
}
