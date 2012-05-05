﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hanasu.Services.Song.Lyric_Data_Sources;
using System.Collections.ObjectModel;
using Hanasu.Services.Song.Album_Info_Data_Source;

namespace Hanasu.Services.Song
{
    public class SongService: IStaticService
    {

        private static Dictionary<string, SongData> SongCache = null;

        static SongService()
        {
            SongCache = new Dictionary<string, SongData>();
        }

        public static bool IsSongAvailable(string songdata, out Uri lyricsUri)
        {
            Hanasu.Services.Logging.LogService.Instance.WriteLog(typeof(SongService), "Checking if song is available from data: " + songdata);

            var newsongdata = CleanSongDataStr(songdata);

            Hanasu.Services.Logging.LogService.Instance.WriteLog(typeof(SongService), "Parsed song data into: " + newsongdata);

            if (SongCache.ContainsKey(newsongdata.ToLower()))
            {
                lyricsUri = SongCache[newsongdata.ToLower()].LyricsUri; ;

                return true;
            }

            var datasource = new LetrasTerraLyricDataSource();

            var bits = newsongdata.Split(new string[] { " - " }, StringSplitOptions.None);


            string lyrics = null;

            try
            {

                if (datasource.GetLyrics(bits[0].Trim(' ', ' '), bits[1].Trim(' ', ' '), out lyrics, out lyricsUri))
                {
                    var song = new SongData();

                    song.Artist = bits[0].Trim(' ');
                    song.TrackTitle = bits[1].Trim(' ');
                    song.Lyrics = lyrics;
                    song.LyricsUri = lyricsUri;

                    new MSMetaServices().GetAlbumInfo(ref song);

                    SongCache.Add(newsongdata.ToLower(), song);

                    return true;
                }
                else if (datasource.GetLyrics(bits[1].Trim(' ', ' '), bits[0].Trim(' ', ' '), out lyrics, out lyricsUri))
                {
                    var song = new SongData();

                    song.Artist = bits[1].Trim(' ');
                    song.TrackTitle = bits[0].Trim(' ');
                    song.Lyrics = lyrics;
                    song.LyricsUri = lyricsUri;

                    new MSMetaServices().GetAlbumInfo(ref song);

                    SongCache.Add(newsongdata.ToLower(), song);

                    return true;
                }
            }
            catch (Exception)
            {
            }
            lyricsUri = null;

            return false;
        }
        public static SongData GetSongData(string songdata)
        {
            if (SongCache.ContainsKey(CleanSongDataStr(songdata).ToLower()))
                return SongCache[CleanSongDataStr(songdata).ToLower()];

            Uri lyricsUrl = null;
            if (IsSongAvailable(songdata, out lyricsUrl))
                return SongCache[CleanSongDataStr(songdata).ToLower()];

            return null;
        }
        private static string CleanSongDataStr(string songdata)
        {
            if (songdata.Contains("~"))
                songdata = songdata.Substring(0, songdata.IndexOf("~"));

            songdata = Regex.Replace(songdata, @"\(.+?(\)|$)", "");

            songdata = songdata.Trim('\n', ' ');

            return songdata;
        }
    }
}