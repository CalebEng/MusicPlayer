using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;
using Winforms = System.Windows.Forms;

namespace MusicPlayer
{
    public partial class MainWindow : Window
    {
        bool running = false;
        private string musicFolderPath = string.Empty;
        private AudioFileReader audioFile;
        private WaveOutEvent outputDevice;

        private List<string> playlist = new List<string>();
        private List<Playlist> playlists = new();
        private Playlist currentPlaylist;
        private List<SongInfo> librarySongs = new();

        private int currentTrackIndex = 0;
        private bool loopPlaylist = false;
        private bool loopSong = false;



        DispatcherTimer timer = new DispatcherTimer();
        public MainWindow()
        {
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += Timer_Tick;
            timer.Start();

            musicFolderPath = Properties.Settings.Default.LastFolder;
            loop.Content = "No Loop";
            Shuffle.Content = "noShuff";

            if (!string.IsNullOrEmpty(musicFolderPath) && Directory.Exists(musicFolderPath))
            {
                LoadPlaylist(musicFolderPath);
                string songFile = currentPlaylist.Songs[currentTrackIndex].FilePath;
                if (songFile != null)
                {
                    LoadSongInfo(songFile);
                }
            }
            Volume.Value = Properties.Settings.Default.Volume;
        }

        private void PlaySong(string filePath)
        {
            if (outputDevice != null)
            {
                outputDevice.PlaybackStopped -= outputDevice_PlaybackStopped;
                outputDevice?.Stop();
                outputDevice?.Dispose();
            }
            audioFile?.Dispose();

            outputDevice = new WaveOutEvent();
            audioFile = new AudioFileReader(filePath);

            outputDevice.Init(audioFile);
            outputDevice.Volume = (float)(Volume.Value / 100.0);

            outputDevice.PlaybackStopped += outputDevice_PlaybackStopped;
            outputDevice.Play();
        }

        private string GetFirstSong(string folder)
        {
            var extensions = new[] { ".mp3", ".wav", ".flac", ".aac", ".m4a"};

            return Directory.EnumerateFiles(folder,"*.*", SearchOption.AllDirectories)
                            .FirstOrDefault(file => extensions.Contains(Path.GetExtension(file).ToLower()));
        }

        // Event handler for the Find Folder button click event
        //Open a folder browser dialog to allow the user to select a music folder when the Find Folder button is clicked
        private void FindFolder(object sender, RoutedEventArgs e)
        {
            using(var dialog = new Winforms.FolderBrowserDialog())
            {
                dialog.Description = "Select Music Folder";
                dialog.UseDescriptionForTitle = true;
                
                if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    musicFolderPath = dialog.SelectedPath;

                    Properties.Settings.Default.LastFolder = musicFolderPath;
                    Properties.Settings.Default.Save();
                    LoadPlaylist(musicFolderPath);
                    string songFile = GetFirstSong(musicFolderPath);
                    if (songFile != null)
                    {
                        LoadSongInfo(songFile);
                    }
                    if(songFile == null)
                    {
                        SongName.Text = "No songs found in folder";
                        ArtistName.Text = "Error";
                    }
                }
            }
        }

        private void LoadPlaylist(string folder)
        {
            var extensions = new[] { ".mp3", ".wav", ".flac", ".aac", ".m4a" };
            playlist = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                                .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                                .ToList();

            var newPlaylist = new Playlist
            {
                Name = "All Songs"
            };

            foreach(String path in playlist)
            {
                try
                {
                    var tagFile = TagLib.File.Create(path);

                    newPlaylist.Songs.Add(new SongInfo { 
                        Artist = tagFile.Tag.FirstPerformer, 
                        Title = tagFile.Tag.Title, 
                        Album = tagFile.Tag.Album, 
                        Length = tagFile.Properties.Duration.ToString(@"mm\:ss"), 
                        FilePath = path 
                    });
                }
                catch
                {
                    continue;
                }
            }
            playlists.Clear();
            playlists.Add(newPlaylist);
            currentPlaylist = newPlaylist;
            PlaylistSelection.ItemsSource = playlists;

            currentTrackIndex = 0;
            LibraryGrid.ItemsSource = currentPlaylist.Songs;
        }

        // Event handler for the Play button click event
        //Play the selected music track when the Play button is clicked
        //swap the text of the Play button to "Pause" and change the event handler to handle the Pause functionality
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (audioFile != null)
            {
                outputDevice.Volume = (float)(Volume.Value / 100.0);
            }
            if (audioFile == null) {
                if (currentPlaylist.Songs.Count == 0)
                {
                    SongName.Text = "No songs found in folder";
                    ArtistName.Text = "Error";
                    return;
                }
                string songFile = currentPlaylist.Songs[currentTrackIndex].FilePath;

                LoadSongInfo(songFile);
                PlaySong(songFile);
                Play.Tag = "/Art/pause.png";
                Play.Uid = "Art/pause-highlight.png";
                running = true;
                return;
            }

            if (running)
            {
                // Pause the music
                outputDevice.Stop();
                Play.Tag = "/Art/play.png";
                Play.Uid = "Art/play-highlight.png";
                running = false;
                
            }
            else
            {
                // Play the music
                outputDevice.Play();
                Play.Tag = "/Art/pause.png";
                Play.Uid = "Art/pause-highlight.png";
                running = true;
                
            }
            
        }

        private void outputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!running) return;
                if (currentTrackIndex == currentPlaylist.Songs.Count - 1 && loopPlaylist == false)
                {
                    Play.Tag = "/Art/play.png";
                    Play.Uid = "Art/play-highlight.png";
                    running = false;

                    if (audioFile != null)
                    {
                        audioFile.Position = 0;
                    }
                }
                else if (loopSong)
                {
                    if (audioFile != null)
                    {
                        audioFile.Position = 0;
                    }
                    PlaySong(currentPlaylist.Songs[currentTrackIndex].FilePath);
                    Play.Tag = "/Art/pause.png";
                    Play.Uid = "Art/pause-highlight.png";
                    running = true;
                }
                else
                {
                    currentTrackIndex = (currentTrackIndex + 1) % currentPlaylist.Songs.Count;
                    string nextSong = currentPlaylist.Songs[currentTrackIndex].FilePath;
                    LoadSongInfo(nextSong);
                    PlaySong(nextSong);

                    Play.Tag = "/Art/pause.png";
                    Play.Uid = "Art/pause-highlight.png";
                    running = true;
                }
            });
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (currentPlaylist.Songs.Count == 0) return;

            currentTrackIndex = (currentTrackIndex + 1) % currentPlaylist.Songs.Count;

            string nextSong = currentPlaylist.Songs[currentTrackIndex].FilePath;
            LoadSongInfo(nextSong);
            PlaySong(nextSong);

            Play.Tag = "/Art/pause.png";
            Play.Uid = "Art/pause-highlight.png";
            running = true;
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPlaylist.Songs.Count == 0) return;
            currentTrackIndex = (currentTrackIndex - 1 + currentPlaylist.Songs.Count) % currentPlaylist.Songs.Count;

            string prevSong = currentPlaylist.Songs[currentTrackIndex].FilePath;
            LoadSongInfo(prevSong);
            PlaySong(prevSong);

            Play.Tag = "/Art/pause.png";
            Play.Uid = "Art/pause-highlight.png";
            running = true;
        }
       
        private void LoadSongInfo(String filePath)
        {
            var file = TagLib.File.Create(filePath);

            SongName.Text = file.Tag.Title;

            ArtistName.Text = file.Tag.FirstPerformer;

            byte[] artBytes = null;
            if(file.Tag.Pictures.Length > 0)
            {
                artBytes = file.Tag.Pictures[0].Data.Data;
            }

            AlbumArt.Source = LoadAlbumArt(artBytes);

        }
        private BitmapImage LoadAlbumArt(byte[] data)
        {
            if (data == null) return null;

            using(var ms = new MemoryStream(data))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }

        private void ProgressBar_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(SongProgress.Value);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFile != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                SongProgress.Value = audioFile.CurrentTime.TotalSeconds;
                SongProgress.Maximum = audioFile.TotalTime.TotalSeconds;
                SongLength.Text = $"{audioFile.CurrentTime:mm\\:ss} / {audioFile.TotalTime:mm\\:ss}";
            }
        }

        private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(audioFile != null)
            {
                outputDevice.Volume = (float)(Volume.Value/100.0);
            }
            Properties.Settings.Default.Volume = Volume.Value;
            Properties.Settings.Default.Save();
        }

        private void Loop_Click(object sender, RoutedEventArgs e)
        {
            if (loop.Content =="No Loop")
            {
                loop.Content = "Loop";

                loop.Tag = "/Art/loop.png";
                loop.Uid = "Art/loop-highlight.png";

                loopPlaylist = true;
                loopSong = false;
            }
            else if (loop.Content == "Loop")
            {
                loop.Content = "Loop 1";

                loop.Tag = "/Art/loop-song.png";
                loop.Uid = "Art/loop-song-highlight.png";

                loopPlaylist = false;
                loopSong = true;
            }
            else
            {
                loop.Content = "No Loop";

                loop.Tag = "/Art/no-loop.png";
                loop.Uid = "Art/no-loop-highlight.png";

                loopPlaylist = false;
                loopSong = false;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            if(Shuffle.Content == "noShuff")
            {
                Shuffle.Content = "shuff";
                Shuffle.Tag = "/Art/shuffle.png";
                Shuffle.Uid = "/Art/shuffle-highlight.png";
            }
            else
            {
                Shuffle.Content = "noShuff";
                Shuffle.Tag = "/Art/no-shuffle.png";
                Shuffle.Uid = "/Art/no-shuffle-highlight.png";
            }
        }

        private void Library_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.Visibility == Visibility.Collapsed) 
            { 
                LibraryGrid.Visibility = Visibility.Visible;
                PlayerView.Visibility = Visibility.Collapsed;
                Playlists_section.Visibility = Visibility.Collapsed;
                Playlists.Content = "Playlists";
                Library.Content = "Close";
            }
            else
            {
                LibraryGrid.Visibility = Visibility.Collapsed;
                PlayerView.Visibility = Visibility.Visible;
                Playlists_section.Visibility = Visibility.Collapsed;
                Playlists.Content = "Playlists";
                Library.Content = "Library";
            }
        }

        private void Playlists_Click(object sender, RoutedEventArgs e)
        {
            if(Playlists_section.Visibility == Visibility.Collapsed)
            {
                Playlists_section.Visibility = Visibility.Visible;
                Playlists.Content = "Close";
            }
            else
            {
                Playlists_section.Visibility = Visibility.Collapsed;
                Playlists.Content = "Playlists";
            }
        }

        private void PlaylistSelection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if(PlaylistSelection.SelectedItem is  Playlist selected)
            {
                currentPlaylist = selected;

                LibraryGrid.ItemsSource = currentPlaylist.Songs;

                currentTrackIndex = 0;

                if (currentPlaylist.Songs.Count == 0)
                {
                    SongName.Text = "No songs in playlist";
                    ArtistName.Text = "Error";
                }
                else
                {
                    string songFile = currentPlaylist.Songs[currentTrackIndex].FilePath;

                    LoadSongInfo(songFile);
                }

            }
        }

        private void NewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var newPlaylist = new Playlist
            {
                Name = "New Playlist " + playlists.Count
            };
            playlists.Add(newPlaylist);

            PlaylistSelection.ItemsSource = null;
            PlaylistSelection.ItemsSource = playlists;

            PlaylistSelection.SelectedItem = newPlaylist;

            currentPlaylist = newPlaylist;
            LibraryGrid.ItemsSource= currentPlaylist.Songs;
        }
    }
}