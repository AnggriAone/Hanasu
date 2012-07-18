﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using Hanasu.Core;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Hanasu.Services.Friends
{
    [Serializable]
    public class FriendConnection
    {
        public const int Port = 46318;
        public const string STATUS_CHANGED = "STATUS_CHANGED";
        public const string CHAT_MESSAGE = "CHAT_MESSAGE";
        public const string PRESENCE_ONLINE = "PRESENCE_ONLINE";
        public const string PRESENCE_OFFLINE = "PRESENCE_OFFLINE";
        public const string AVATAR_SET = "AVATAR_SET";
        internal FriendConnection(string userName, string IP, int KEY, bool UseUDP = true, bool IfTCPIsHost = false)
        {
            UserName = userName;
            IPAddress = IP;
            Key = KEY;
            Initiate(IP);
            IsUDP = UseUDP;
            IsTCPHost = IfTCPIsHost;
        }

        public void Initiate(string IP)
        {
            // Socket = new UdpClient();
            EndPoint = new IPEndPoint(System.Net.IPAddress.Parse(IP), 46318);

            //Socket.Connect(EndPoint);

            IsConnected = false;

            if (!IsUDP)
            {
                if (IsTCPHost)
                    Hanasu.Services.Friends.FriendsService.InitializeSocketTCP();
                else
                    InitializeSocketTCPClient();
            }
            else
                IsConnected = true;

        }

        internal void InitializeSocketTCPClient()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(t =>
            {
            a: try
                {
                    Socket = new TcpClient();
                    Socket.Connect(EndPoint);

                    TCPStream = Socket.GetStream();

                    IsConnected = true;
                    TCP_ConnectionIsOnline();
                    HandleTcpConnection();
                }
                catch (Exception)
                {


                }
                IsConnected = false;

                TCP_ConnectionIsOffline();

                Socket.Close();
                Thread.Sleep(5000);
                goto a;
            }));
        }

        internal void TCP_ConnectionIsOnline()
        {
            SetPresence(true);
            Hanasu.Services.Friends.FriendsService.Instance.GetFriendViewFromConnection(this).Status = "Online";
            IsConnected = true;
        }
        internal void TCP_ConnectionIsOffline()
        {
            //SetPresence(false);
            try
            {
                Hanasu.Services.Friends.FriendsService.Instance.GetFriendViewFromConnection(this).Status = "Offline";
            }
            catch (Exception) { }
            IsConnected = false;
        }


        #region For TCP based connections only
        [NonSerialized]
        internal NetworkStream TCPStream = null;

        internal void HandleTcpConnection()
        {
            while (GetIsSocketConnected())
            {
                while (_IsDataAvailableTCP)
                {
                    try
                    {
                        ReadTCPData();
                    }
                    catch (Exception)
                    {
                        GetIsSocketConnected();
                        if (IsConnected == true)
                        {
                            Thread.Sleep(2000);
                            continue;
                        }
                        else
                            return;
                    }
                }

                Thread.Sleep(2000);
            }
        }
        private void ReadTCPData()
        {
            byte[] bits = new byte[512];
            Socket.GetStream().Read(bits, 0, bits.Length);

            var data = System.Text.UnicodeEncoding.Unicode.GetString(bits);

            if (string.IsNullOrEmpty(data.Replace("\0", ""))) return;

            var spl = data.Split(new char[] { ' ' }, 3);

            var sentKey = int.Parse(spl[1]);

            if (sentKey == Key)
            {
                Hanasu.Services.Friends.FriendsService.Instance.HandleReceievedData(this, spl[0], spl[2].Substring(1).Replace("\0", ""));
            }
            else
                return;
        }
        private bool GetIsSocketConnected()
        {
            if (Socket == null || Socket.Client == null)
            {
                IsConnected = false;
                _IsDataAvailableTCP = false;
                return false;
            }
            else
            {
                bool socketStatus = Socket.Client.Poll(50,
                    SelectMode.SelectRead); //http://social.msdn.microsoft.com/Forums/en-US/netfxnetcom/thread/f4c3d019-aecd-4fc6-9dea-680f04faa900/

                switch (socketStatus)
                {
                    case true:
                        if (Socket.Client.Available == 0) //Checks if the socket is disconnected.
                        {
                            _IsDataAvailableTCP = false;
                            IsConnected = false;
                            return false;
                        }
                        else
                        {
                            _IsDataAvailableTCP = true;
                            IsConnected = true;
                            return true;
                        }
                    case false:
                        _IsDataAvailableTCP = false;
                        IsConnected = true;
                        return true;
                }
                return false;
            }
        }
        [NonSerialized]
        private bool _IsDataAvailableTCP = false;
        #endregion

        public string UserName { get; private set; }
        internal IPEndPoint EndPoint = null;
        public int Key { get; private set; }
        public string IPAddress { get; private set; }

        public bool IsConnected { get; private set; }

        public bool IsUDP { get; set; }
        public bool IsTCPHost { get; set; }

        public string AvatarUrl { get; set; }

        //private UdpClient Socket = null;
        [NonSerialized]
        internal volatile TcpClient Socket = null;


        [NonSerialized]
        private string _status = "Offline";
        public string Status
        {
            get { return _status; }
            set { _status = value; }
        }
        public static readonly System.Windows.DependencyProperty StatusProperty = System.Windows.DependencyProperty.Register("Status", typeof(string), typeof(FriendConnection));

        private void SendRaw(string msg)
        {
            var data = System.Text.UnicodeEncoding.Unicode.GetBytes(msg);

            if (IsUDP)
                Hanasu.Services.Friends.FriendsService.GlobalSocketUDP.Send(data, data.Length, EndPoint);
            else
            {
                GetIsSocketConnected();
                if (IsConnected && Socket != null)
                    Socket.GetStream().Write(data, 0, data.Length);
                else
                    throw new Exception("Not connected!");
            }
        }
        public void SendData(string data, string type = "NOTIF")
        {
            SendRaw(
                String.Format("{0} {1} :{2}", type, Key.ToString(), data));
        }
        public void SendStatusChange(string status)
        {
            SendData(status, STATUS_CHANGED);
        }
        public void SendChatMessage(string msg)
        {
            SendData(msg, CHAT_MESSAGE);
        }
        public void SetPresence(bool isOnline = true)
        {
            if (isOnline)
                SendData("ONLINE", PRESENCE_ONLINE);
            else
                SendData("OFFLINE", PRESENCE_OFFLINE);
        }
        public void SetAvatar(string avatarurl)
        {
            SendData(avatarurl, AVATAR_SET);
        }
        /*public void SendObject(string type, object obj)
        {
            using (var ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);

                var data = System.Text.UnicodeEncoding.Unicode.GetBytes(
                    String.Format("{0} {1} :", type, Key.ToString()));

                var objbits = ms.ToArray();

                byte[] rv = new byte[data.Length + objbits.Length];
                System.Buffer.BlockCopy(data, 0, rv, 0, data.Length);
                System.Buffer.BlockCopy(objbits, 0, rv, data.Length, objbits.Length);

                Hanasu.Services.Friends.FriendsService.GlobalSocket.Send(rv, rv.Length, EndPoint);
            }
        }*/

        [NonSerialized]
        public bool IsOnline = false;
    }
}
