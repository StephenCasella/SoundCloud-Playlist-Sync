using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Soundcloud_Playlist_Downloader
{
    class PlaylistSync
    {
        public bool IsError { get; private set; }

        public enum DownloadMode { Playlist, Favorites };

        public int TotalSongsToDownload { get; private set; }
        public int TotalSongsDownloaded { get; private set; }

        private object SongsToDownloadLock = new object();
        private object SongsDownloadedLock = new object();

        public bool IsActive { get; set; }

        public PlaylistSync()
        {
            ResetProgress();
        }

        private void verifyParameters(Dictionary<string, string> parameters)
        {
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Value))
                {
                    IsError = true;
                    throw new Exception(string.Format("{0} must be specified", parameter.Key));
                }
            }
        }

        internal void Synchronize(string url, DownloadMode mode, string directory, bool deleteRemovedSongs, string clientId)
        {
            verifyParameters(
                new Dictionary<string, string>()
                {
                    {"URL", url},
                    {"Directory", directory},
                    {"Client ID", clientId}
                }
            );

            ResetProgress();

            switch (mode)
            {
                case DownloadMode.Playlist:
                    // determine whether it is an api url or a normal url. if it is a normal url, get the api url from it
                    // and then call SynchronizeFromPlaylistAPIUrl. Otherwise just call that method directly
                    string apiPlaylistUrl = null;
                    if (!url.Contains("api.soundcloud.com"))
                    {
                        apiPlaylistUrl = determineAPIPlaylistUrlForNormalUrl(url, clientId);
                    }
                    else 
                    {
                        apiPlaylistUrl = url;
                    }
                    SynchronizeFromPlaylistAPIUrl(apiPlaylistUrl, clientId, directory, deleteRemovedSongs);
                    break;
                case DownloadMode.Favorites:
                    // get the username from the url and then call SynchronizeFromProfile
                    string username = parseUserIdFromProfileUrl(url);
                    SynchronizeFromProfile(username, clientId, directory, deleteRemovedSongs);
                    break;
                default:
                    IsError = true;
                    throw new NotImplementedException("Unknown download mode");
            }
        }

        private string determineAPIPlaylistUrlForNormalUrl(string url, string clientId)
        {

            // parse the username from the url
            string username = parseUserIdFromProfileUrl(url);
            string playlistName = null;
            try
            {
                // parse the playlist name from the url
                string startingPoint = "/sets/";
                int startingIndex = url.IndexOf(startingPoint) + startingPoint.Length;
                int endingIndex = url.Substring(startingIndex).Contains("/") ?
                    url.Substring(startingIndex).IndexOf("/") + startingIndex :
                    url.Length;
                playlistName = url.Substring(startingIndex, endingIndex - startingIndex);
            }
            catch (Exception e)
            {
                IsError = true;
                throw new Exception("Invalid playlist url: " + e.Message);
            }

            // hit the users/username/playlists endpoint and match the playlist on the permalink
            string userUrl = "https://api.soundcloud.com/users/" + username + "/playlists";

            return "https://api.soundcloud.com/playlists/" +
                retrievePlaylistId(userUrl, playlistName, clientId);
        }

        private string retrievePlaylistId(string userApiUrl, string playlistName, string clientId)
        {
            // grab the xml from the url, parse each playlist out, match the name based on the
            // permalink, and return the id of the matching playlist.
            // a method already exists for downloading xml, so use that and refactor this to not have
            // the client id embedded in the url

            // get the xml associated with the playlist from the soundcloud api
            string playlistsXML = RetrieveXML(userApiUrl, clientId);

            string playlistId = null;

            try
            {
               XDocument document = XDocument.Parse(playlistsXML);

                XElement playlists = document.Element("playlists");

                foreach (XElement node in playlists.Nodes())
                {

                    if (node.Name == "playlist")
                    {

                        string id = null;
                        string permalink = null;

                        foreach (XElement playlistElement in node.Elements())
                        {
                            if (playlistElement.Name == "id")
                            {
                                id = playlistElement.Value;
                            }
                            else if (playlistElement.Name == "permalink")
                            {
                                permalink = playlistElement.Value;
                            }
                            else if (id != null && permalink != null)
                            {
                                break;
                            }
                        }

                        if (permalink == playlistName)
                        {
                            playlistId = id;
                            break;
                        }
                    }
                }

                if (playlistId == null)
                {
                    IsError = true;
                    throw new Exception("Unable to find a matching playlist");
                }
                else
                {
                    return playlistId;
                }
            }
            catch (Exception e)
            {
                IsError = true;
                throw new Exception("Error parsing user playlist information: " + e.Message);
            }

        }

        private string parseUserIdFromProfileUrl(string url)
        {
            try
            {
                string startingPoint = "soundcloud.com/";
                int startingIndex = url.IndexOf(startingPoint) + startingPoint.Length;
                int endingIndex = url.Substring(startingIndex).Contains("/") ?
                    url.Substring(startingIndex).IndexOf("/") + startingIndex :
                    url.Length;

                return url.Substring(startingIndex, endingIndex - startingIndex);
            }
            catch (Exception e)
            {
                IsError = true;
                throw new Exception("Invalid profile url: " + e.Message);
            }
        }


        internal void SynchronizeFromProfile(string username, string clientId, string directoryPath, bool deleteRemovedSongs)
        {
            // hit the /username/favorites endpoint for the username in the url, then download all the tracks


            // get the xml associated with the playlist from the soundcloud api
            string tracksXML = RetrieveXML("https://api.soundcloud.com/users/" + username + "/favorites", clientId);

            if (!string.IsNullOrWhiteSpace(tracksXML))
            {
                // get the songs embedded in the playlist
                IList<Song> allSongs = ParseSongsFromFavoritesXML(tracksXML, directoryPath, clientId);
                Synchronize(allSongs, clientId, directoryPath, deleteRemovedSongs);

            }
            else
            {
                IsError = true;
                throw new Exception("Playlist not found");
            }

        }

        private void Synchronize(IList<Song> songs, string clientId, string directoryPath, bool deleteRemovedSongs)
        {
            // determine which songs should be downloaded
            IList<Song> songsToDownload = DetermineSongsToDownload(directoryPath, songs);
            TotalSongsToDownload = songsToDownload.Count;

            // determine which songs should be deleted
            if (deleteRemovedSongs)
            {
                DeleteRemovedSongs(directoryPath, songs);
            }

            // download the relevant songs
            IList<Song> songsDownloaded = DownloadSongs(songsToDownload, clientId);

            // update the manifest
            UpdateSyncManifest(songsDownloaded, directoryPath);

            // validation
            if (songsDownloaded.Count != songsToDownload.Count && IsActive)
            {
                IsError = true;
                throw new Exception("Some songs failed to download. Please try again.");
            }
        }


        internal void SynchronizeFromPlaylistAPIUrl(string playlistApiUrl, string clientId, string directoryPath, bool deleteRemovedSongs)
        {

            // get the xml associated with the playlist from the soundcloud api
            string playlistXML = RetrieveXML(playlistApiUrl, clientId);

            if (!string.IsNullOrWhiteSpace(playlistXML))
            {
                // get the songs embedded in the playlist
                IList<Song> allSongs = ParseSongsFromPlaylistXML(playlistXML, directoryPath, clientId);
                Synchronize(allSongs, clientId, directoryPath, deleteRemovedSongs);
                
            }
            else
            {
                IsError = true;
                throw new Exception("Playlist not found");
            }
        }

        private void ResetProgress()
        {
            TotalSongsDownloaded = 0;
            TotalSongsToDownload = 0;
            IsActive = true;
            IsError = false;
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
                IsError = true;
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

                        try
                        {
                            WebRequest request = WebRequest.Create(song.EffectiveDownloadUrl);

                            request.Method = "HEAD";
                            using (WebResponse response = request.GetResponse())
                            {
                                extension = Path.GetExtension(response.Headers["Content-Disposition"]
                                    .Replace("attachment;filename=", "").Replace("\"", ""));
                            }
                        }
                        catch (Exception)
                        {
                            // the download link might be invalid
                            WebRequest request = WebRequest.Create(song.StreamUrl);

                            request.Method = "HEAD";
                            using (WebResponse response = request.GetResponse())
                            {
                                extension = Path.GetExtension(response.Headers["Content-Disposition"]
                                    .Replace("attachment;filename=", "").Replace("\"", ""));
                            }
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
                IsError = true;
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

        private IList<Song> ParseSongsFromTracksElement(XElement tracksElement, string localPath, string clientId)
        {
            IList<Song> songs = new List<Song>();

            foreach (XElement track in tracksElement.Elements())
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
                            song.StreamUrl = attribute.Value + "?client_id=" + clientId;
                        }
                        else if (attribute.Name == "download-url")
                        {
                            song.DownloadUrl = attribute.Value + "?client_id=" + clientId;
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
                        !string.IsNullOrWhiteSpace(song.EffectiveDownloadUrl))
                    {


                        song.LocalPath = Path.Combine(Path.Combine(localPath, song.Username), song.Title);
                        songs.Add(song);
                    }
                    else
                    {
                        // song is not streamable or downloadble
                    }

                }
            }

            return songs;
        }

        private IList<Song> ParseSongsFromPlaylistXML(string playlistXML, string localPath, string clientId)
        {
            IList<Song> songs = new List<Song>();

            try
            {
                XDocument document = XDocument.Parse(playlistXML);

                XElement playlist = document.Element("playlist");

                foreach (XElement node in playlist.Nodes())
                {
                    if (node.Name == "tracks")
                    {
                        songs = ParseSongsFromTracksElement(node, localPath, clientId);
                    }
                }
            }
            catch (Exception)
            {
                IsError = true;
                throw new Exception("An error occurred parsing the playlist XML. Are you using the correct link " +
                    "(i.e. https//api.soundcloud.com/playlist/...)? See " +
                    "https://originaltechsolutions.blogspot.com/2013/11/soundcloud-playlist-downloader-free.html" +
                    " for instructions on how to determine the API link for a playlist.");
            }

            return songs;
        }


        private IList<Song> ParseSongsFromFavoritesXML(string tracksXML, string localPath, string clientId) {
                        
            IList<Song> songs = new List<Song>();

            try
            {
                XDocument document = XDocument.Parse(tracksXML);

                XElement tracks = document.Element("tracks");
                songs = ParseSongsFromTracksElement(tracks, localPath, clientId);

            }
            catch (Exception)
            {
                IsError = true;
                throw new Exception("An error occurred parsing the favorites XML. Are you using the correct link?");
            }

            return songs;
        }

        private string RetrieveXML(string url, string clientId)
        {
            string xml = null;

            try
            {
                using (WebClient client = new WebClient()) 
                {
                    xml = client.DownloadString(url + "?client_id=" + clientId);
                }
            }
            catch (Exception)
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

    public class Song
    {

        private string _title = null;
        public string Title
        {
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
        public string EffectiveDownloadUrl
        {
            get
            {
                string url = !string.IsNullOrWhiteSpace(DownloadUrl) ?
                    DownloadUrl : StreamUrl;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url.Replace("\r", "").Replace("\n", "");
                }
                else
                {
                    return null;
                }
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

        public string Sanitize(string input)
        {
            Regex regex = new Regex(@"[^\w\s\d-]");
            return input != null ? 
                regex.Replace(input.Replace("&amp;", "and")
                    .Replace("&", "and").Replace(".", "_"),
                   string.Empty)
                : null;
        }
    }
}
