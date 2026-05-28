using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Winforms = System.Windows.Forms;
using TagLib;
using NAudio.Wave;
using System.Windows.Threading;

namespace MusicPlayer
{
    public partial class MainWindow : Window
    {
        bool running = false;
        private string musicFolderPath = string.Empty;
        private AudioFileReader audioFile;
        private WaveOutEvent outputDevice;
        private List<string> playlist = new List<string>();
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


            if(!string.IsNullOrEmpty(musicFolderPath) && Directory.Exists(musicFolderPath))
            {
                LoadPlaylist(musicFolderPath);
                string songFile = playlist[currentTrackIndex];
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
            currentTrackIndex = 0;
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
                if (playlist.Count == 0)
                {
                    SongName.Text = "No songs found in folder";
                    ArtistName.Text = "Error";
                    return;
                }
                string songFile = playlist[currentTrackIndex];

                LoadSongInfo(songFile);
                PlaySong(songFile);
                Play.Content = "Pause";
                running = true;
                return;
            }

            if (running)
            {
                // Pause the music
                outputDevice.Stop();
                Play.Content = "Play";
                running = false;
                
            }
            else
            {
                // Play the music
                outputDevice.Play();
                Play.Content = "Pause";
                running = true;
                
            }
            
        }

        private void outputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!running) return;
                if (currentTrackIndex == playlist.Count - 1 && loopPlaylist == false)
                {
                    Play.Content = "Play";
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
                    PlaySong(playlist[currentTrackIndex]);
                    Play.Content = "Pause";
                    running = true;
                }
                else
                {
                    currentTrackIndex = (currentTrackIndex + 1) % playlist.Count;
                    string nextSong = playlist[currentTrackIndex];
                    LoadSongInfo(nextSong);
                    PlaySong(nextSong);

                    Play.Content = "Pause";
                    running = true;
                }
            });
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;

            currentTrackIndex = (currentTrackIndex + 1) % playlist.Count;

            string nextSong = playlist[currentTrackIndex];
            LoadSongInfo(nextSong);
            PlaySong(nextSong);

            Play.Content = "Pause";
            running = true;
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;
            currentTrackIndex = (currentTrackIndex - 1 + playlist.Count) % playlist.Count;

            string prevSong = playlist[currentTrackIndex];
            LoadSongInfo(prevSong);
            PlaySong(prevSong);

            Play.Content = "Pause";
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
    }
}