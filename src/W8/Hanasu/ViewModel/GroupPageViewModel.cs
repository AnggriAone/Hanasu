﻿using Crystal.Core;
using Hanasu.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Media;
using Hanasu.Extensions;
using System.Windows.Input;
using Crystal.Localization;

namespace Hanasu.ViewModel
{
    public class GroupPageViewModel : Crystal.Dynamic.AutoIPNotifyingBaseViewModel
    {
        public GroupPageViewModel()
        {
        }

        public override void OnNavigatedTo(KeyValuePair<string, object>[] argument = null)
        {
            var args = (KeyValuePair<string, object>)argument[0];

            Stations = new ObservableCollection<Station>();

            var name = args.Value.ToString();

            GroupName = LocalizationManager.GetLocalizedValue("Group" + name);

            foreach (var stat in ((App)BaseCrystalApplication.Current)
                .AvailableStations
                .Where(x => x.UnlocalizedFormat == name)
                .OrderBy(x => x.Title))
                Stations.Add(stat);

            RaisePropertyChanged(x => this.Stations);
        }

        public string GroupName { get; set; }

        public ObservableCollection<Station> Stations { get; set; }
    }
}
