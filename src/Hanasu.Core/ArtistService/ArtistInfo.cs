﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hanasu.Core.ArtistService
{
    public struct ArtistInfo
    {
        public string Name { get; set; }
        public string Bio { get; set; }
        public ArtistType Type { get; set; }
        public DateTime Debut { get; set; }

        public Uri ArtistImageUrl { get; set; }


        public string SOLO_Realname { get; set; }
        public ArtistGender SOLO_Gender { get; set; }
        public DateTime SOLO_Birthday { get; set; }

        public string GROUP_BandName { get; set; }
        public string GROUP_BandType { get; set; }
    }
}