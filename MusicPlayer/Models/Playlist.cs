using System;
using System.Collections.Generic;
using System.Text;

namespace MusicPlayer
{
    public class Playlist
    {
        public string Name { get; set; }
        public List<SongInfo> Songs { get; set; } = new();
    }
}
