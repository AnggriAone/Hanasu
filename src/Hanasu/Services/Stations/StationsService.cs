﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Hanasu.Core;
using System.Xml.Linq;
using System.Timers;
using Hanasu.Services.Logging;
using System.IO;
using System.Net;
using System.Xml;
using System.Windows;
using System.Collections;
using System.Text.RegularExpressions;
using Hanasu.Services.Song;

namespace Hanasu.Services.Stations
{
    public class StationsService : BaseINPC, IStaticService
    {
        static StationsService()
        {
            if (Instance == null)
                Initialize();
        }
        public static void Initialize()
        {
            if (Instance == null)
                Instance = new StationsService();
        }
        public static StationsService Instance { get; private set; }

        private Timer timer = null; //Timer is used for 'streaming updates' of new radio stations. That way, new  stations are added without reloading Hanasu.

        public StationsService()
        {

            LogService.Instance.WriteLog(this,
                "Stations Service initialized.");

            Stations = new ObservableCollection<Station>();
            CustomStations = new ObservableCollection<Station>();
            timer = new Timer();

            if (!App.CheckIfSafeStart()) return;

            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Hanasu\\"))
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Hanasu\\");

            StationsCacheDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Hanasu\\Cache\\";
            if (!Directory.Exists(StationsCacheDir))
                Directory.CreateDirectory(StationsCacheDir);



            System.Threading.Tasks.Task.Factory.StartNew(() =>
                LoadStationsFromRepo()).ContinueWith((tk) => tk.Dispose());

            LoadCustomStations();

            if (Hanasu.Services.Settings.SettingsService.Instance.UpdateStationsLive)
            {
                timer.Elapsed += timer_Elapsed;

                timer.Interval = 60000 * 30; //30 minutes

                timer.Start();
            }
        }

        private void LoadCustomStations()
        {
            System.Windows.Application.Current.Exit += Current_Exit;

            if (File.Exists(CustomStationsFile))
            {
                try
                {
                    RadioFormat dummie = 0;
                    dynamic stats = null;

                    var doc = XDocument.Load(CustomStationsFile);
                    stats = from x in doc.Element("Stations").Elements("Station")
                            select ParseStation(ref dummie, x);

                    foreach (var item in stats)
                        CustomStations.Add(item);
                }
                catch (Exception)
                {
                }

                OnPropertyChanged("CustomStations");
            }
        }

        void Current_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            System.Windows.Application.Current.Exit -= Current_Exit;

            SaveCustomStations();
        }
        void SaveCustomStations()
        {
            XElement stations = new XElement("Stations",
                from x in CustomStations
                select new XElement("Station",
                    new XElement("Name", x.Name),
                    new XElement("DataSource", x.DataSource),
                    new XElement("Homepage", x.Homepage),
                    new XElement("Format", Enum.GetName(typeof(RadioFormat), x.Format)),
                    new XElement("City", x.City),
                    new XElement("Language", Enum.GetName(typeof(StationLanguage), x.Language)),
                    new XElement("Cacheable", x.Cacheable.ToString()),
                    new XElement("ExplitcitExtension", x.ExplicitExtension == null ? "" : x.ExplicitExtension),
                    new XElement("Schedule", x.ScheduleUrl == null ? "" : x.ScheduleUrl.ToString(), new XAttribute("type", Enum.GetName(typeof(StationScheduleType), x.ScheduleType))),
                    new XElement("Logo", x.Logo == null ? "" : x.Logo.ToString())));

            var doc = new XDocument(new XDeclaration("1.0", "Unicode", "yes"), stations);
            doc.Save(CustomStationsFile);

        }

        public static string StationsCacheDir { get; private set; }
        public string CustomStationsFile { get { return StationsCacheDir + "CustomStations.xml"; } }
        public string StationsCachedFile { get { return StationsCacheDir + "Stations.xml"; } }
        public string StationsUrl { get { return "https://raw.github.com/Amrykid/Hanasu/master/src/Hanasu/Stations.xml"; } }

        internal void LoadStationsFromRepo()
        {
            try
            {
                LogService.Instance.WriteLog(this,
    "BEGIN: Station polling operation.");

                System.Windows.Application.Current.Dispatcher.Invoke(new EmptyParameterizedDelegate((t) =>
                    {
                        if (StationFetchStarted != null)
                            StationFetchStarted((StationsService)t, null);
                    }), this);

                Status = StationsServiceStatus.Polling;

                //System.Threading.Thread.Sleep(10000);

                System.Windows.Application.Current.Dispatcher.Invoke(new Hanasu.Services.Notifications.NotificationsService.EmptyDelegate(() =>
                    {
                        Stations.Clear();
                    }));


                //var doc = XDocument.Load("https://raw.github.com/Amrykid/Hanasu/master/src/Hanasu/Stations.xml");


                RadioFormat dummie = 0;
                dynamic stats = null;

                if (File.Exists(StationsCachedFile))
                {
                    try
                    {
                        var doc = XDocument.Load(StationsCachedFile);
                        stats = from x in doc.Element("Stations").Elements("Station")
                                select ParseStation(ref dummie, x);
                    }
                    catch (Exception)
                    {
                        if (NetworkUtils.IsConnectedToInternet())
                        {
                            stats = from x in StreamStationsXml()
                                    select ParseStation(ref dummie, x);
                        }
                        else
                        {
                            MessageBox.Show("Unable to load cached Stations file. Also, unable to connect to online Stations file. Hanasu will now exit.");
                            Application.Current.Shutdown();
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(new EmptyParameterizedDelegate2((f, g) =>
                    {
                        foreach (var x in (IEnumerable<Station>)f)
                            Stations.Add(x);

                        OnPropertyChanged("Stations");

                        if (StationFetchCompleted != null)
                            StationFetchCompleted((StationsService)g, null);
                    }), stats, this);
                }
                else
                {
                    if (NetworkUtils.IsConnectedToInternet())
                    {

                        stats = from x in StreamStationsXml()
                                select ParseStation(ref dummie, x);

                        DownloadStationsToCache();
                    }
                    else
                    {
                        MessageBox.Show("Unable to connect to online Stations file. Hanasu will now exit.");
                        Application.Current.Shutdown();
                    }


                    var finalstats = new List<Station>();
                    foreach (Station st in stats)
                    {
                        var o = st;
                        if (o.Cacheable)
                        {
                            try
                            {
                                var s = CheckAndDownloadCacheableStation(ref o);

                                finalstats.Add(s);
                            }
                            catch (Exception)
                            {
                                finalstats.Add(o);
                            }
                        }
                        else
                            finalstats.Add(o);
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(new EmptyParameterizedDelegate2((f, g) =>
                        {
                            if (Stations.Count > 0)
                                Stations.Clear();

                            foreach (var x in (List<Station>)f)
                                Stations.Add(x);

                            OnPropertyChanged("Stations");

                            if (StationFetchCompleted != null)
                                StationFetchCompleted((StationsService)g, null);
                        }), finalstats, this);

                }

            }
            catch (Exception) { }

            LogService.Instance.WriteLog(this,
                "END: Station polling operation.");
            Status = StationsServiceStatus.Idle;
        }

        internal static Station CheckAndDownloadCacheableStation(ref Station st)
        {
            //Checks if its possible to cache the playlist file.
            var s = st;
            if (st.ExplicitExtension != "" && Preprocessor.PreprocessorService.CheckIfPreprocessingIsNeeded(st.DataSource, st.ExplicitExtension) && st.Cacheable && st.StationType == StationType.Radio)
            {
                var cachefile = StationsCacheDir + st.Name + "_" + st.DataSource.LocalPath.Substring(st.DataSource.LocalPath.LastIndexOf("/") + 1);
                if (!File.Exists(cachefile))
                    using (WebClient wc = new WebClient())
                    {
                        try
                        {
                            wc.DownloadFile(st.DataSource, cachefile);
                            s.LocalStationFile = new Uri(cachefile);
                        }
                        catch (Exception)
                        {

                        }
                    }
                else
                    s.LocalStationFile = new Uri(cachefile);
            }
            return s;
        }

        internal void DownloadStationsToCache()
        {
            using (var wc = new WebClient())
            {
                wc.DownloadFileAsync(new Uri(StationsUrl), StationsCachedFile);
            }
        }
        internal System.Threading.Tasks.Task DownloadStationsToCacheAsync()
        {
            return System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    using (var wc = new WebClient())
                    {
                        wc.DownloadFile(new Uri(StationsUrl), StationsCachedFile);
                    }
                }).ContinueWith(t => t.Dispose());
        }

        internal static Station ParseStation(ref RadioFormat dummie, XElement x)
        {
            return new Station()
            {
                Name = x.Element("Name").Value,
                DataSource = new Uri(x.Element("DataSource").Value),
                Homepage = string.IsNullOrEmpty(x.Element("Homepage").Value) ? null : new Uri(x.Element("Homepage").Value),
                Format = (Enum.TryParse<RadioFormat>(x.Element("Format").Value, out dummie) == true ?
                    (RadioFormat)Enum.Parse(typeof(RadioFormat), x.Element("Format").Value) :
                    RadioFormat.Mix),
                City = x.Element("City").Value,
                ExplicitExtension = x.ContainsElement("ExplicitExtension") ? x.Element("ExplicitExtension").Value : null,
                StationType = x.ContainsElement("StationType") ? (StationType)Enum.Parse(typeof(StationType), x.Element("StationType").Value) : StationType.Radio,
                Language = x.ContainsElement("Language") ? (StationLanguage)Enum.Parse(typeof(StationLanguage), x.Element("Language").Value) : StationLanguage.Unknown,
                Cacheable = x.ContainsElement("Cacheable") ? bool.Parse(x.Element("Cacheable").Value) : false,
                ScheduleType = x.ContainsElement("Schedule") ? (StationScheduleType)Enum.Parse(typeof(StationScheduleType), x.Element("Schedule").Attribute("type").Value) : StationScheduleType.none,
                ScheduleUrl = x.ContainsElement("Schedule") ? (string.IsNullOrEmpty(x.Element("Schedule").Value) ? null : new Uri(x.Element("Schedule").Value)) : null,
                Logo = x.ContainsElement("Logo") ? (string.IsNullOrEmpty(x.Element("Logo").Value) ? null : new Uri(x.Element("Logo").Value)) : null,
                UseAlternateSongTitleFetching = x.ContainsElement("UseAlternateSongTitleFetching") ? bool.Parse(x.Element("UseAlternateSongTitleFetching").Value) : false
            };
        }

        internal static IEnumerable<XElement> StreamStationsXml()
        {
            using (XmlReader reader = XmlReader.Create("https://raw.github.com/Amrykid/Hanasu/master/src/Hanasu/Stations.xml"))
            {
                reader.MoveToContent();
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "Station")
                            {
                                XElement el = XElement.ReadFrom(reader)
                                                      as XElement;
                                if (el != null)
                                    yield return el;
                            }
                            break;
                    }
                }
                reader.Close();
            }
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (NetworkUtils.IsConnectedToInternet())
            {
                int before = Stations.Count;

                System.Windows.Application.Current.Dispatcher.Invoke(
                    new Hanasu.Services.Notifications.NotificationsService.EmptyDelegate(() =>
                        {
                            LoadStationsFromRepo();
                        }));

                if (Stations.Count > before)
                {
                    Hanasu.Services.Notifications.NotificationsService.AddNotification("Stations Updated",
                        (Stations.Count - before).ToString() + " station(s) added.", 4000, true);

                    DownloadStationsToCache();
                }
            }
        }

        private delegate void EmptyParameterizedDelegate(object obj);
        private delegate void EmptyParameterizedDelegate2(object obj, object obj2);

        public ObservableCollection<Station> Stations { get; private set; }
        public ObservableCollection<Station> CustomStations { get; private set; }

        public StationsServiceStatus Status { get; private set; }

        public event EventHandler StationFetchStarted;
        public event EventHandler StationFetchCompleted;

        private static List<string> cached_Shoutcasturls = new List<string>();
        public static bool GetIfShoutcastStation(Hashtable playerAttributes)
        {
            try
            {
                if (playerAttributes.ContainsKey("SourceURL"))
                {
                    var url = (string)playerAttributes["SourceURL"];

                    if (cached_Shoutcasturls.Contains(url)) return true;

                    if (!url.StartsWith("http") && !url.StartsWith("https")) return false;

                    var html = Hanasu.Core.HtmlTextUtility.GetHtmlFromUrl2(url);

                    var res = html.Contains("SHOUTcast D.N.A.S. Status</font>");

                    if (res)
                        cached_Shoutcasturls.Add(url);

                    return res;

                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public static Dictionary<string, string> GetShoutcastStationSongHistory(Station station, Hashtable playerAttributes)
        {
            var his = new Dictionary<string, string>();

            var url = (string)playerAttributes["SourceURL"];
            if (url.EndsWith("/") == false)
                url += "/";
            url += "played.html";

            var html = Hanasu.Core.HtmlTextUtility.GetHtmlFromUrl2(url);

            var songtable = Regex.Matches(html, "<table.+?>.+?</table>", RegexOptions.Singleline | RegexOptions.Compiled)[1];
            var entries = Regex.Matches(songtable.Value,
                "<tr>.+?</tr>",
                RegexOptions.Singleline | RegexOptions.Compiled);

            for (int i = 1; i < entries.Count; i++)
            {
                var entry = entries[i];
                var bits = Regex.Matches(
                        Regex.Replace(
                            entry.Value, "<b>Current Song</b>", "", RegexOptions.Compiled | RegexOptions.Singleline),
                    "<td>.+?(</td>|</tr>)", RegexOptions.Compiled | RegexOptions.Singleline);

                var key = Regex.Replace(bits[0].Value, "<.+?>", "", RegexOptions.Singleline | RegexOptions.Compiled).Trim();
                var val = Regex.Replace(bits[1].Value, "<.+?>", "", RegexOptions.Singleline | RegexOptions.Compiled).Trim();
                if (his.ContainsKey(key) == false)
                    his.Add(key,
                        val);
            }


            return his;
        }
        public static DateTime GetShoutcastStationCurrentSongStartTime(Station station, Hashtable playerAttributes)
        {
            var things = GetShoutcastStationSongHistory(station, playerAttributes);
            var lastime = things.Keys.First();

            var time = DateTime.Parse(lastime, null, System.Globalization.DateTimeStyles.AssumeLocal);
            return time;
        }
        public static SongData GetShoutcastStationCurrentSong(Station station, Hashtable playerAttributes)
        {
            var url = (string)playerAttributes["SourceURL"];

            var html = Hanasu.Core.HtmlTextUtility.GetHtmlFromUrl2(url.ToString());
            var songtable = Regex.Matches(html, "<table.+?>.+?</table>", RegexOptions.Singleline | RegexOptions.Compiled)[2];
            var entries = Regex.Matches(songtable.Value,
                "<tr>.+?</tr>",
                RegexOptions.Singleline | RegexOptions.Compiled);

            var songEntry = entries[entries.Count - 1];
            var songData = Regex.Match(songEntry.Value, "<b>.+?</b>", RegexOptions.Singleline | RegexOptions.Compiled).Value;
            songData = Regex.Replace(songData, "<.+?>", "", RegexOptions.Compiled | RegexOptions.Singleline).Trim();
            songData = Hanasu.Services.Song.SongService.CleanSongDataStr(songData);

            Uri lyrics = null;
            if (Hanasu.Services.Song.SongService.IsSongAvailable(songData, station, out lyrics))
                return Hanasu.Services.Song.SongService.GetSongData(songData, station);
            else
                if (Hanasu.Services.Song.SongService.IsSongTitle(songData, station))
                    return Hanasu.Services.Song.SongService.ParseSongData(songData, station);
                else throw new Exception();
        }
    }
}

//http://www.surfmusic.de/country/japan.html