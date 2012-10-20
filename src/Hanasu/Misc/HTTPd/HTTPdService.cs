﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Windows;

namespace Hanasu.Misc.HTTPd
{
    public static class HTTPdService
    {
        private static TcpListener tcpListener = new TcpListener(IPAddress.Any, 4477);

        public static void Start()
        {
            if (IsRunning) throw new InvalidOperationException();

            try
            {
                tcpListener.Start();

                IsRunning = true;

                tcpListener.BeginAcceptTcpClient(new AsyncCallback(HandleRequest), null);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void HandleRequest(IAsyncResult res)
        {
            var tcp = tcpListener.EndAcceptTcpClient(res);

            tcpListener.BeginAcceptTcpClient(new AsyncCallback(HandleRequest), null);

            HandleConnection(tcp);
        }

        private const string BaseDir = "/Misc/HTTPd/Web App/Hanasu-Web-App";

        private static void HandleConnection(TcpClient tcp)
        {
            try
            {
                string head = "";
                while (head.EndsWith("\r\n\r\n") == false)
                {
                    head += ReadSocket(ref tcp);

                    if (head == "") return;
                }

                var headLines = head.Replace("\r\n", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var firstLine = headLines[0].Split(' ');
                string method = firstLine[0].ToUpper(), file = firstLine[1], httpversion = firstLine[2];

                bool keepalive = headLines.First(t => t.StartsWith("Connection: ")) == null ?
                    false :
                    headLines.First(t => t.StartsWith("Connection: ")).Substring("Connection: ".Length).ToLower() == "keep-alive";

                bool close = !keepalive;

                if (method == "GET" || method == "HEAD")
                {
                    #region GET/HEAD
                    //hard coded atm
                    //TODO: make this not hard-coded.

                    var fileToGet = "";

                    switch (file.ToLower())
                    {
                        case "/":
                            {
                                fileToGet = "/index.html";
                            }
                            break;
                        default:
                            fileToGet = file;
                            break;
                    }

                    try
                    {
                        if (method == "GET")
                        {

                            if (_urlHandlers.ContainsKey(fileToGet))
                            {
                                if (_urlHandlers[fileToGet].Item1 == HttpRequestType.GET || _urlHandlers[fileToGet].Item1 == HttpRequestType.GETANDPOST)
                                {
                                    if (HttpUrlHandler != null)
                                        WriteSocket(ref tcp, HttpResponseBuilder.OKResponse(HttpUrlHandler(fileToGet, HttpRequestType.GET, null).ToString(), HttpMimeTypes.Html, close), close);
                                }
                                else
                                {
                                    WriteSocket(ref tcp, HttpResponseBuilder.MethodNotAllowedResponse(), close);
                                }
                            }
                            else
                            {
                                //is a included file and not a handler.

                                var s = Application.GetResourceStream(
                                    new Uri(@BaseDir + fileToGet, UriKind.RelativeOrAbsolute));

                                using (var sr = new System.IO.StreamReader(s.Stream))
                                {
                                    var mimeType = s.ContentType;

                                    if (fileToGet.EndsWith(".js"))
                                        mimeType = HttpMimeTypes.Javascript;
                                    else if (fileToGet.EndsWith(".css"))
                                        mimeType = HttpMimeTypes.Css;

                                    WriteSocket(ref tcp, HttpResponseBuilder.OKResponse(sr.ReadToEnd(), mimeType, close), close);

                                    sr.Close();
                                }

                                s.Stream.Close();
                            }
                        }
                        else if (method == "HEAD")
                        {
                            WriteSocket(ref tcp, HttpResponseBuilder.OKResponse("", HttpMimeTypes.Html, close), close);
                        }


                    }
                    catch (Exception)
                    {
                        WriteSocket(ref tcp, HttpResponseBuilder.NotFoundResponse(), close);
                    }
                    #endregion
                }
                else if (method == "POST")
                {
                    if (HttpPostReceived != null)
                        HttpPostReceived(file, null);

                    if (_urlHandlers.ContainsKey(file))
                    {
                        if (_urlHandlers[file].Item1 == HttpRequestType.POST || _urlHandlers[file].Item1 == HttpRequestType.GETANDPOST)
                        {
                            object postData = null; // to be implemented

                            if (HttpUrlHandler != null)
                                WriteSocket(ref tcp, HttpResponseBuilder.OKResponse(HttpUrlHandler(file, HttpRequestType.POST, postData).ToString(), HttpMimeTypes.Html, close), close);
                        }
                        else
                        {
                            WriteSocket(ref tcp, HttpResponseBuilder.MethodNotAllowedResponse(), close);
                        }
                    }
                    else
                    {
                        WriteSocket(ref tcp, HttpResponseBuilder.NotFoundResponse(), close);
                    }

                }

                if (keepalive)
                    HandleConnection(tcp);
            }
            catch (Exception)
            {
                tcp.Close();
            }


        }
        private static void WriteSocket(ref TcpClient tcp, string data, bool close = true)
        {
            try
            {
                tcp.Client.Send(
                    System.Text.ASCIIEncoding.ASCII.GetBytes(data));
            }
            catch (Exception)
            {
            }

            if (close)
                tcp.Close();
        }
        private static string ReadSocket(ref TcpClient tcp)
        {
            tcp.Client.ReceiveTimeout = 100;

            //This is used for converting bytes to strings.
            byte[] b = new byte[100];
            int k = tcp.Client.Receive(b);
            StringBuilder msg = new StringBuilder();
            for (int i = 0; i < k; i++)
                msg.Append(Convert.ToChar(b[i]));
            return msg.ToString();
        }

        public static void Stop()
        {
            if (!IsRunning) throw new InvalidOperationException();

            try
            {
                tcpListener.Stop();

                IsRunning = false; ;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static bool IsRunning { get; private set; }

        public delegate void HttpPostReceivedHandler(string file, object postdata);
        public static event HttpPostReceivedHandler HttpPostReceived;

        public delegate object HttpUrlHandlerHandler(string relativeUrl, HttpRequestType type, object postdata);
        public static event HttpUrlHandlerHandler HttpUrlHandler;

        public static void RegisterUrlHandler(string relativeUrl, HttpRequestType type, string helpInformation)
        {
            if (_urlHandlers.ContainsKey(relativeUrl)) throw new Exception("Url is already registered.");

            _urlHandlers.Add(relativeUrl, new Tuple<HttpRequestType, string>(type, helpInformation));
        }

        private static Dictionary<string, Tuple<HttpRequestType, string>> _urlHandlers = new Dictionary<string, Tuple<HttpRequestType, string>>();

        public static IEnumerable<KeyValuePair<string, Tuple<HttpRequestType, string>>> GetUrlHandlers()
        {
            return _urlHandlers.ToArray();
        }

        public enum HttpRequestType
        {
            GET = 0,
            HEAD = 1,
            POST = 2,
            GETANDPOST = 3
        }
    }
}