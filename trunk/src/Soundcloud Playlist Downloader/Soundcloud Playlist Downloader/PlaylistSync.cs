using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Soundcloud_Playlist_Downloader
{
    class PlaylistSync
    {
        public int TotalSongsToDownload { get; private set; }
        public int TotalSongsDownloaded { get; private set; }

        private object SongsToDownloadLock = new object();
        private object SongsDownloadedLock = new object();

        public bool IsActive { get; set; }

        public PlaylistSync()
        {
            ResetProgress();
        }

        internal void Synchronize(string playlistUrl, string apiKey, string directoryPath, bool deleteRemovedSongs)
        {
            ResetProgress();

            // get the xml associated with the playlist from the soundcloud api
            string playlistXML = RetrievePlaylistXML(playlistUrl, apiKey);

            if (!string.IsNullOrWhiteSpace(playlistXML))
            {
                // get the songs embedded in the playlist
                IList<Song> allSongs = ParseSongsFromPlaylistXML(playlistXML, directoryPath, apiKey);

                // determine which songs should be downloaded
                IList<Song> songsToDownload = DetermineSongsToDownload(directoryPath, allSongs);
                TotalSongsToDownload = songsToDownload.Count;

                // determine which songs should be deleted
                if (deleteRemovedSongs) 
                {
                    DeleteRemovedSongs(directoryPath, allSongs);
                }

                // download the relevant songs
                IList<Song> songsDownloaded = DownloadSongs(songsToDownload, apiKey);

                // update the manifest
                UpdateSyncManifest(songsDownloaded, directoryPath);

                // validation
                if (songsDownloaded.Count != songsToDownload.Count && IsActive)
                {
                    throw new Exception("Some songs failed to download. Please try again.");
                }
            }
            else
            {
                throw new Exception("Playlist not found");
            }
        }

        private void ResetProgress()
        {
            TotalSongsDownloaded = 0;
            TotalSongsToDownload = 0;
            IsActive = true;
        }

        private void UpdateSyncManifest(IList<Song> songsDownloaded, string directoryPath)
        {
            IList<string> content = new List<string>();

            foreach (Song song in songsDownloaded)
            {
                content.Add(song.EffectiveDownloadUrl + "," + song.LocalPath);
            }

            try
            {
                string manifestPath = DetermineManifestPath(directoryPath);
                if (File.Exists(manifestPath))
                {
                    File.AppendAllLines(manifestPath, content);
                }
                else
                {
                    File.WriteAllLines(manifestPath, content);
                }
            }
            catch (Exception)
            {
                throw new Exception("Unable to update manifest");
            }

        }

        private IList<Song> DownloadSongs(IList<Song> songsToDownload, string apiKey)
        {
            IList<Song> songsDownloaded = new List<Song>();
            object songLock = new object();

            Parallel.ForEach(songsToDownload, 
                new ParallelOptions() {MaxDegreeOfParallelism = 3},
                song =>
            {
                try
                {
                    if (DownloadSong(song, apiKey))
                    {

                        lock (songLock)
                        {
                            songsDownloaded.Add(song);
                        }
                    }

                }
                catch (Exception)
                {
                    // Song failed to download
                }
                
            });


            return songsDownloaded;
        }

        [SafeRetry]
        private bool DownloadSong(Song song, string apiKey)
        {
            bool downloaded = false;
            if (IsActive)
            {
                using (WebClient client = new WebClient())
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(song.LocalPath));

                    if (song.IsHD)
                    {
                        string extension = null;

                        WebRequest request = WebRequest.Create(song.EffectiveDownloadUrl);

                        request.Method = "HEAD";
                        using (WebResponse response = request.GetResponse())
                        {
                            extension = Path.GetExtension(response.Headers["Content-Disposition"]
                                .Replace("attachment;filename=", "").Replace("\"", ""));
                        }

                        song.LocalPath += extension;
                    }
                    else
                    {
                        song.LocalPath += ".mp3";
                    }

                    client.DownloadFile(song.EffectiveDownloadUrl, song.LocalPath);


                    // metadata tagging
                    TagLib.File tagFile = TagLib.File.Create(song.LocalPath);
                    tagFile.Tag.AlbumArtists = new string[] { song.Username };
                    tagFile.Tag.Performers = new string[] { song.Username };
                    tagFile.Tag.Title = song.Title;
                    tagFile.Tag.Genres = new string[] { song.Genre };
                    tagFile.Tag.Comment = song.Description;
                    tagFile.Save();

                    lock (SongsDownloadedLock)
                    {
                        ++TotalSongsDownloaded;
                        downloaded = true;
                    }
                }
            }

            return downloaded;

        }

        private void DeleteRemovedSongs(string directoryPath, IList<Song> allSongs)
        {
            IList<Song> songsToDelete = new List<Song>();
            string manifestPath = DetermineManifestPath(directoryPath);

            try
            {
                if (File.Exists(manifestPath))
                {
                    string[] songsDownloaded = File.ReadAllLines(manifestPath);

                    IList<string> newManifest = new List<string>();

                    foreach (string songDownloaded in songsDownloaded)
                    {
                        string localPath = ParseSongPath(songDownloaded);

                        if (!allSongs.Select(song => song.LocalPath).Contains(localPath))
                        {
                            File.Delete(localPath);
                        }
                        else
                        {
                            newManifest.Add(songDownloaded);
                        }
                    }

                    // the manifest is updated again later, but might as well update it here
                    // to save the deletions in event of crash or abort
                    File.WriteAllLines(manifestPath, newManifest);
                }
            }
            catch (Exception)
            {
                throw new Exception("Unable to read manifest to determine songs to delete");
            }
            
        }

        private IList<Song> DetermineSongsToDownload(string directoryPath, IList<Song> allSongs)
        {
            string manifestPath = DetermineManifestPath(directoryPath);

            IList<string> streamUrls = new List<string>();

            if (File.Exists(manifestPath))
            {
                foreach (string song in File.ReadAllLines(manifestPath))
                {
                    streamUrls.Add(ParseSongPath(song));
                }
            }

            return allSongs.Where(s => !streamUrls.Contains(s.EffectiveDownloadUrl)).ToList();
        }

        private IList<Song> ParseSongsFromPlaylistXML(string playlistXML, string localPath, string apiKey)
        {
            IList<Song> songs = new List<Song>();
            XDocument document = XDocument.Parse(playlistXML);
            
            XElement playlist = document.Element("playlist");

            foreach (XElement node in playlist.Nodes())
            {
                if (node.Name == "tracks")
                {

                    foreach (XElement track in node.Elements())
                    {
                        if (track.Name == "track")
                        {

                            Song song = new Song();

                            foreach (XElement attribute in track.Elements())
                            {

                                if (attribute.Name == "title")
                                {
                                    song.Title = attribute.Value;
                                }
                                else if (attribute.Name == "user")
                                {
                                    foreach (XElement userNode in attribute.Elements())
                                    {
                                        if (userNode.Name == "username")
                                        {
                                            song.Username = userNode.Value;
                                            break;
                                        }
                                    }
                                }
                                else if (attribute.Name == "stream-url")
                                {
                                    song.StreamUrl = attribute.Value + "?client_id=" + apiKey;
                                }
                                else if (attribute.Name == "download-url")
                                {
                                    song.DownloadUrl = attribute.Value + "?client_id=" + apiKey;
                                }
                                else if (attribute.Name == "genre")
                                {
                                    song.Genre = attribute.Value;
                                }
                                else if (attribute.Name == "description")
                                {
                                    song.Description = attribute.Value;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(song.Title) &&
                                !string.IsNullOrWhiteSpace(song.Username) &&
                                !string.IsNullOrWhiteSpace(song.StreamUrl))
                            {


                                song.LocalPath = Path.Combine(Path.Combine(localPath, song.Username), song.Title);
                                songs.Add(song);
                            }

                        }
                    }

                }
            }            

            return songs;
        }

        private string RetrievePlaylistXML(string playlistUrl, string apiKey)
        {
            string xml = null;

            try
            {
                using (WebClient client = new WebClient()) 
                {
                    xml = client.DownloadString(playlistUrl + "?client_id=" + apiKey);
                }
            }
            catch (Exception e)
            {
                // Nothing to do here
            }

            return xml;
        }

        private string DetermineManifestPath(string directoryPath)
        {
            return Path.Combine(directoryPath, "manifest");
        }

        private string ParseSongPath(string csv)
        {
            return csv != null && csv.IndexOf(',') >= 0 ? csv.Split(',')[0] : csv;
        }
    }

    class Song
    {

        private string _title = null;
        public string Title { 
            get 
            { 
                return EffectiveDownloadUrl == DownloadUrl ? _title + "_High_Quality" : _title; 
            }
            set
            {
                _title = Sanitize(value);
            }
        }

        private string _username = null;
        public string Username 
        { 
            get 
            { 
                return _username; 
            } 
            set 
            { 
                _username = Sanitize(value);
            } 
        }

        public string StreamUrl { get; set; }
        public string DownloadUrl { get; set; }
        public bool IsHD { get { return DownloadUrl == EffectiveDownloadUrl; } }
        public string EffectiveDownloadUrl { 
            get 
            { 
                return (!string.IsNullOrWhiteSpace(DownloadUrl) ? 
                    DownloadUrl : StreamUrl).Replace("\r", "").Replace("\n",""); 
            } 
        }

        private string _path = null;
        public string LocalPath 
        {
            get 
            {
                return _path;
            } 
            set 
            { 
                _path = value; 
            } 
        }
        
        public string Genre { get; set; }
        public string Description { get; set; }

        private string Sanitize(string input)
        {
            // TODO: replace this with a whitelist regex
            return input != null ? input.Replace("&amp;", "and")
                    .Replace(";", "").Replace("[", "")
                    .Replace("]", "").Replace("(", " ")
                    .Replace(")", " ").Replace("*", "")
                    .Replace("!", "").Replace("&", "and")
                    .Replace(":", "").Replace("\"", "")
                    .Replace(".", "_") : null;
        }
    }
}
