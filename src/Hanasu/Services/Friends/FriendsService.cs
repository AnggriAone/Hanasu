﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hanasu.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Windows.Data;
using Hanasu.Windows;
using System.Windows;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using Hanasu.Services.Events;
using System.Xml.Linq;

namespace Hanasu.Services.Friends
{
    public class FriendsService : BaseINPC, IStaticService
    {
        static FriendsService()
        {
            if (!IsInitialized)
                Initialize();
        }
        public static void Initialize()
        {
            if (IsInitialized) return;

            Instance = new FriendsService();

            var dir = Hanasu.Services.Stations.StationsService.StationsCacheDir;

            Instance.FriendsDBFile = dir.Replace("\\Cache", "") + "Friends.bin";

            System.Windows.Application.Current.Exit += Current_Exit;

            Hanasu.Services.Events.EventService.AttachHandler(Events.EventType.Settings_Created, HandleSettingsCreated);
            Hanasu.Services.Events.EventService.AttachHandler(Events.EventType.Settings_Loaded, HandleSettingsLoaded);
            Hanasu.Services.Events.EventService.AttachHandler(Events.EventType.Settings_Saving, HandleSettingsSaving);

            LoadFriends();

            GlobalSocket = new UdpClient(FriendConnection.Port, AddressFamily.InterNetwork);

            ThreadPool.QueueUserWorkItem(new WaitCallback(t =>
            {
                HandleConnection();
            }));

            BroadcastPresence(true);
            Hanasu.Services.Events.EventService.AttachHandler(Events.EventType.Station_Changed,
                e =>
                {
                    var e2 = (Hanasu.MainWindow.StationEventInfo)e;

                    foreach (FriendView f in Instance.Friends)
                        f.Connection.SendStatusChange("Now listening to " + e2.CurrentStation.Name);
                });

            IsInitialized = true;
        }

        private static void BroadcastPresence(bool isOnline)
        {
            foreach (var con in Instance.Friends)
                con.Connection.SetPresence(isOnline);
        }
        private static void BroadcastAvatar(string url)
        {
            foreach (var con in Instance.Friends)
                con.Connection.SetAvatar(url);
        }


        private static void HandleSettingsCreated(EventInfo ei)
        {
            Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo sdei = (Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo)ei;
        }
        private static void HandleSettingsLoaded(EventInfo ei)
        {
            Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo sdei = (Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo)ei;

            AvatarUrl = sdei.SettingsElement.ContainsElement("AvatarUrl") ? sdei.SettingsElement.Element("AvatarUrl").Value : null;
            BroadcastAvatar(_avatarurl);

        }
        private static void HandleSettingsSaving(EventInfo ei)
        {
            Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo sdei = (Hanasu.Services.Settings.SettingsService.SettingsDataEventInfo)ei;

            sdei.SettingsElement.Add(
                new XElement("AvatarUrl", AvatarUrl));
        }

        #region Socket/UDP Stuff
        internal static UdpClient GlobalSocket { get; set; }

        private static bool PollForData()
        {
            return Hanasu.Services.Friends.FriendsService.GlobalSocket.Available > 0;
        }
        private static void HandleConnection()
        {
            try
            {
                if (PollForData())
                {
                    ReadData();
                }

                Thread.Sleep(5000);

                HandleConnection();
            }
            catch (Exception)
            {
                return;
            }
        }
        private static void ReadData()
        {
            try
            {
                IPEndPoint e = null;
                var data = GlobalSocket.Receive(ref e);
                var str = System.Text.ASCIIEncoding.ASCII.GetString(data);

                var spl = str.Split(new char[] { ' ' }, 3);

                var sentKey = int.Parse(spl[1]);

                var person = Instance.Friends.First(f => f.Connection.IPAddress == e.Address.ToString());

                if (person != null)
                {
                    if (sentKey == person.Connection.Key)
                    {
                        Hanasu.Services.Friends.FriendsService.Instance.HandleReceievedData(person.Connection, spl[0], spl[2].Substring(1));
                    }
                    else
                        return;
                }

            }
            catch (Exception)
            {
            }

        }
        #endregion

        private static void LoadFriends()
        {
            if (System.IO.File.Exists(Instance.FriendsDBFile))
            {
                try
                {
                    using (var fs = new FileStream(Instance.FriendsDBFile, FileMode.OpenOrCreate))
                    {
                        IFormatter bf = new BinaryFormatter();

                        var friends = (ObservableCollection<FriendConnection>)bf.Deserialize(fs);
                        Instance.Friends = new ObservableCollection<FriendView>();

                        foreach (FriendConnection fc in friends)
                            Instance.Friends.Add(new FriendView(fc));

                        fs.Close();
                    }

                    foreach (FriendView f in Instance.Friends)
                        f.Connection.Initiate(f.Connection.IPAddress);

                    Instance.OnPropertyChanged("Friends");
                }
                catch (Exception)
                {
                    Instance.Friends = new ObservableCollection<FriendView>();
                }
            }
            else
            {
                Instance.Friends = new ObservableCollection<FriendView>();
            }
        }

        static void Current_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            System.Windows.Application.Current.Exit -= Current_Exit;

            if (Instance.Friends.Count > 0)
            {
                BroadcastPresence(false);

                using (var fs = new FileStream(Instance.FriendsDBFile, FileMode.OpenOrCreate))
                {
                    var x = new ObservableCollection<FriendConnection>();
                    foreach (var fv in Instance.Friends)
                        x.Add(fv.Connection);

                    IFormatter bf = new BinaryFormatter();
                    bf.Serialize(fs, x);
                    fs.Close();
                }
            }
        }

        private static string _avatarurl = null;
        public static string AvatarUrl { get { return _avatarurl; } set { _avatarurl = value; BroadcastAvatar(_avatarurl); } }

        public static bool IsInitialized { get; private set; }
        public static FriendsService Instance { get; private set; }

        public string FriendsDBFile { get; private set; }
        public ObservableCollection<FriendView> Friends { get; set; }
        private List<FriendChatWindow> ChatWindows = new List<FriendChatWindow>();

        internal void HandleReceievedData(FriendConnection friendConnection, string type, string p)
        {
            switch (type.ToUpper())
            {
                case FriendConnection.STATUS_CHANGED:
                    {

                        var view = GetFriendViewFromConnection(friendConnection);
                        view.Status = p;
                        friendConnection.IsOnline = true;

                        Hanasu.Services.Notifications.NotificationsService.AddNotification(friendConnection.UserName + "'s Status",
                            p,
                            3000,
                            true,
                            Notifications.NotificationType.Now_Playing);
                    }
                    break;
                case FriendConnection.CHAT_MESSAGE:
                    {
                        Application.Current.Dispatcher.Invoke(new EmptyDelegate(() =>
                            {
                                friendConnection.IsOnline = true;

                                if (ChatWindows.Any(f => ((FriendConnection)f.DataContext).UserName == friendConnection.UserName))
                                {
                                    var window = ChatWindows.Find(f => ((FriendConnection)f.DataContext).UserName == friendConnection.UserName);

                                    window.HandleMessage(p);

                                    if (window.IsVisible == false)
                                    {
                                        window.Show();

                                        try
                                        {
                                            var file = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.System)).Parent.FullName + "\\Media\\notify.wav";
                                            var s = new System.Media.SoundPlayer(file);
                                            s.PlaySync();
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }

                                    window.Focus();
                                }
                                else
                                {
                                    var window = new FriendChatWindow();
                                    window.DataContext = friendConnection;
                                    window.Show();
                                    window.HandleMessage(p);
                                    ChatWindows.Add(window);
                                }
                            }));

                        break;
                    }
                case FriendConnection.PRESENCE_ONLINE:
                    {
                        if (!friendConnection.IsOnline)
                        {
                            Notifications.NotificationsService.AddNotification("Friend Online",
                                friendConnection.UserName + " is now online!", 3000, true);
                            friendConnection.IsOnline = true;
                        }
                        break;
                    }
                case FriendConnection.PRESENCE_OFFLINE:
                    {
                        if (friendConnection.IsOnline)
                        {
                            Notifications.NotificationsService.AddNotification("Friend Offline",
                                friendConnection.UserName + " is now offline!", 3000, true);

                            var view = GetFriendViewFromConnection(friendConnection);
                            view.Status = "Offline";

                            friendConnection.IsOnline = false;
                        }
                        break;
                    }
                case FriendConnection.AVATAR_SET:
                    {
                        var view = GetFriendViewFromConnection(friendConnection);
                        view.AvatarUrl = p;
                        break;
                    }
            }
        }

        public FriendView GetFriendViewFromConnection(FriendConnection conn)
        {
            return Instance.Friends.First(t => t.Connection == conn);
        }
        public FriendChatWindow GetChatWindow(FriendView friendView)
        {
            return GetChatWindow(friendView.Connection);
        }
        public FriendChatWindow GetChatWindow(FriendConnection friendConnection)
        {
            if (ChatWindows.Any(f => ((FriendConnection)f.DataContext).UserName == friendConnection.UserName))
            {
                var window = ChatWindows.Find(f => ((FriendConnection)f.DataContext).UserName == friendConnection.UserName);
                return window;
            }
            else
            {
                var window = new FriendChatWindow();
                window.DataContext = friendConnection;
                ChatWindows.Add(window);
                return window;
            }
        }

        public void AddFriend(string username, string ip, int key)
        {
            var f = new FriendConnection(username, ip, key);
            Friends.Add(new FriendView(f));
            Instance.OnPropertyChanged("Friends");
        }

    }
    public delegate void EmptyDelegate();
}
