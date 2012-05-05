﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hanasu.Services.Song
{
    public interface ILyricsDataSource
    {
        bool GetLyrics(string artist, string track, out string lyrics, out Uri lyricsUri);
        string WebsiteName { get; }
        string LyricFormatUrl { get; }
    }
}