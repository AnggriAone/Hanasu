﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.Storage.Streams;
using System.IO;
using Windows.UI.Notifications;

namespace Hanasu.NewStationsCheckTask
{
    public sealed class StationCheckerTask: IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            if (_CancelRequested)
                return;

            var deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += new BackgroundTaskCanceledEventHandler(OnCanceled);

            StorageFolder AppFolder = null;
            try
            {
                AppFolder = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("Hanasu");
            }
            catch { }

            if (AppFolder == null)
                AppFolder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("Hanasu");

            using (HttpClient http = new HttpClient())
            {
                var html = await http.GetStringAsync("https://github.com/Amrykid/Hanasu/blob/master/Stations.xml");
                var timeMatches = Regex.Matches(html, "<time.+?</time>");

                var dateTime = timeMatches[0].Value;
                dateTime = dateTime.Substring(dateTime.IndexOf("datetime=") + "datetime=".Length + 1);
                dateTime = dateTime.Substring(0, dateTime.IndexOf("\""));

                var remoteModDate = DateTime.Parse(dateTime, null, System.Globalization.DateTimeStyles.AdjustToUniversal); //parse the last modified day of the repo

                var stationsFile = await AppFolder.GetFileAsync("Stations.xml");
                var info = await stationsFile.GetBasicPropertiesAsync();
                var localModDateNow = info.DateModified.ToUniversalTime();

                //bool beforeOrequalToLocalFileModDate = (utcNow.Year > dateTimeObj.Year) || (utcNow.Year == dateTimeObj.Year && utcNow.DayOfYear >= dateTimeObj.DayOfYear);
                //bool beforeLocalFileModDate = 
                //    (localModDateNow.Year > remoteModDate.Year) //if the file was modified before today
                //    || (localModDateNow.Year == remoteModDate.Year && localModDateNow.DayOfYear > remoteModDate.DayOfYear); 
                bool remoteWasModifiedToday = localModDateNow.Year == remoteModDate.Year && localModDateNow.DayOfYear == remoteModDate.DayOfYear;

                if (remoteWasModifiedToday)
                {
                    //Hasn't update. Show an old update... or not.

                    using (var localStream = await stationsFile.OpenReadAsync())
                    {
                        using (var localNormalStream = localStream.AsStream())
                        {
                            XDocument docLocal = XDocument.Load(localNormalStream);
                            XDocument docRemote = XDocument.Load("https://raw.github.com/Amrykid/Hanasu/master/Stations.xml");

                            if (docRemote.Element("Stations").Elements().Count() >= docLocal.Element("Stations").Elements().Count())
                            {
                                var probableLatest = docRemote.Element("Stations").Elements().Last();

                                var updater = TileUpdateManager.CreateTileUpdaterForApplication();
                                updater.Clear();

                                var tile = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWideImageAndText02);
                                var tilexml = tile.GetXml();
                                tile.GetElementsByTagName("text")[0].InnerText = probableLatest.Element("Name").Value;
                                tile.GetElementsByTagName("text")[1].InnerText = remoteModDate.ToLocalTime().ToString();
                                tile.GetElementsByTagName("image")[0].Attributes.First(x =>
                                    x.NodeName == "src").InnerText = probableLatest.Element("Logo").Value;

                                updater.Update(new TileNotification(tile));
                            }
                        }
                    }
                }
                else
                {
                }
            }

            deferral.Complete();
        }


        volatile bool _CancelRequested = false;
        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            //
            // Indicate that the background task is canceled.
            //

            _CancelRequested = true;
        }
    }
}