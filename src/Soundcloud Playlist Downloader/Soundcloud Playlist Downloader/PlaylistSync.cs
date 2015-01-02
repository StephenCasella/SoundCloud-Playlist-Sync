using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Soundcloud_Playlist_Downloader.JsonPoco;
using Soundcloud_Playlist_Downloader.Properties;

namespace Soundcloud_Playlist_Downloader
{
    class PlaylistSync
    {
        public bool IsError { get; private set; }

        public enum DownloadMode { Playlist, Favorites,Artist };

        public int TotalSongsToDownload { get; private set; }
        public int TotalSongsDownloaded { get; private set; }

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

            string apiURL = null;

            switch (mode)
            {
                case DownloadMode.Playlist:
                    // determine whether it is an api url or a normal url. if it is a normal url, get the api url from it
                    // and then call SynchronizeFromPlaylistAPIUrl. Otherwise just call that method directly
                    
                    if (!url.Contains("api.soundcloud.com"))
                    {
                        apiURL = determineAPIUrlForNormalUrl(url, clientId,"playlists");
                    }
                    else 
                    {
                        apiURL = url;
                    }
                    SynchronizeFromPlaylistAPIUrl(apiURL, clientId, directory, deleteRemovedSongs);
                    break;
                case DownloadMode.Favorites:
                    // get the username from the url and then call SynchronizeFromProfile
                    string username = parseUserIdFromProfileUrl(url);
                    SynchronizeFromProfile(username, clientId, directory, deleteRemovedSongs);
                    break;
                case DownloadMode.Artist:
                    
                    if (!url.Contains("api.soundcloud.com"))
                    {
                        apiURL = determineAPIUrlForNormalUrl(url, clientId,"tracks");
                    }
                    else 
                    {
                        apiURL = url;
                    }
                    SynchronizeFromArtistUrl(apiURL, clientId, directory, deleteRemovedSongs);
                    break;
                default:
                    IsError = true;
                    throw new NotImplementedException("Unknown download mode");
            }
        }

        private string determineAPIUrlForNormalUrl(string url, string clientId,string resulttype)
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
            string userUrl = "https://api.soundcloud.com/users/" + username + "/" + resulttype;

            if (resulttype == "tracks")
            {
                return userUrl;
            }

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
            string playlistsJson = RetrieveJson(userApiUrl, clientId);


            var playlistItems= JsonConvert.DeserializeObject<JsonPoco.PlaylistItem[]>(playlistsJson);

            var playListItem = playlistItems.FirstOrDefault(s => s.permalink == playlistName);

            if (playListItem != null)
            {
                return playListItem.id.ToString();
            }
            else
            {
                IsError = true;
                throw new Exception("Unable to find a matching playlist");
                
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
            string tracksJson = RetrieveJson("https://api.soundcloud.com/users/" + username + "/favorites", clientId);


            if (!string.IsNullOrWhiteSpace(tracksJson))
            {
                // get the tracks embedded in the playlist
                IList<Track> allTracks = JsonConvert.DeserializeObject<Track[]>(tracksJson);
                    
                    //ParseSongsFromFavoritesXML(tracksJson, directoryPath, clientId);
                Synchronize(allTracks, clientId, directoryPath, deleteRemovedSongs);

            }
            else
            {
                IsError = true;
                throw new Exception("Playlist not found");
            }

        }

        private void Synchronize(IList<Track> tracks, string clientId, string directoryPath, bool deleteRemovedSongs)
        {
            // determine which tracks should be downloaded
            IList<Track> tracksToDownload = DetermineTracksToDownload(directoryPath, tracks);
            TotalSongsToDownload = tracksToDownload.Count;

            // determine which tracks should be deleted
            if (deleteRemovedSongs)
            {
                DeleteRemovedTrack(directoryPath, tracks);
            }

            // download the relevant tracks
            IList<Track> songsDownloaded = DownloadSongs(tracksToDownload, clientId);

            // update the manifest
            UpdateSyncManifest(songsDownloaded, directoryPath);

            // validation
            if (songsDownloaded.Count != tracksToDownload.Count && IsActive)
            {
                IsError = true;
                throw new Exception("Some tracks failed to download. Please try again.");
            }
        }


        internal void SynchronizeFromPlaylistAPIUrl(string playlistApiUrl, string clientId, string directoryPath, bool deleteRemovedSongs)
        {

            // get the xml associated with the playlist from the soundcloud api
            string playlistJson = RetrieveJson(playlistApiUrl, clientId);

            if (!string.IsNullOrWhiteSpace(playlistJson))
            {

                var selectedPlaylist = JsonConvert.DeserializeObject<JsonPoco.PlaylistItem>(playlistJson);

                // get the tracks embedded in the playlist
                IList<Track> allTracks = selectedPlaylist.tracks.ToList();
                Synchronize(allTracks, clientId, directoryPath, deleteRemovedSongs);
                
            }
            else
            {
                IsError = true;
                throw new Exception("Playlist not found");
            }
        }


        internal void SynchronizeFromArtistUrl(string artistUrl, string clientId, string directoryPath, bool deleteRemovedSongs)
        {

            // get the xml associated with the playlist from the soundcloud api
            string playlistJson = RetrieveJson(artistUrl, clientId);

            if (!string.IsNullOrWhiteSpace(playlistJson))
            {

                var tracksToDownload = JsonConvert.DeserializeObject<JsonPoco.Track[]>(playlistJson);

                // get the tracks embedded in the playlist
                IList<Track> allTracks = tracksToDownload.ToList();
                Synchronize(allTracks, clientId, directoryPath, deleteRemovedSongs);

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

        private void UpdateSyncManifest(IList<Track> tracksDownloaded, string directoryPath)
        {
            IList<string> content = new List<string>();

            foreach (Track track in tracksDownloaded)
            {
                content.Add(track.EffectiveDownloadUrl + "," + track.LocalPath);
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

        private IList<Track> DownloadSongs(IList<Track> TracksToDownload, string apiKey)
        {
            IList<Track> songsDownloaded = new List<Track>();
            object trackLock = new object();

            Parallel.ForEach(TracksToDownload, 
                new ParallelOptions() {MaxDegreeOfParallelism = Settings.Default.ConcurrentDownloads},
                track =>
            {
                try
                {
                    if (DownloadTrack(track, apiKey))
                    {

                        lock (trackLock)
                        {
                            songsDownloaded.Add(track);
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
        private bool DownloadTrack(Track song, string apiKey)
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
                            WebRequest request = WebRequest.Create(song.EffectiveDownloadUrl + string.Format("?client_id={0}",apiKey));

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
                            WebRequest request = WebRequest.Create(song.stream_url + string.Format("?client_id={0}", apiKey));

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

                    client.DownloadFile(song.EffectiveDownloadUrl+string.Format("?client_id={0}",apiKey), song.LocalPath);


                    // metadata tagging
                    TagLib.File tagFile = TagLib.File.Create(song.LocalPath);
                    tagFile.Tag.AlbumArtists = new string[] { song.Username };
                    tagFile.Tag.Performers = new string[] { song.Username };
                    tagFile.Tag.Title = song.Title;
                    tagFile.Tag.Genres = new string[] { song.genre };
                    tagFile.Tag.Comment = song.description;
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

        private void DeleteRemovedTrack(string directoryPath, IList<Track> allTracks)
        {
            IList<Track> songsToDelete = new List<Track>();
            string manifestPath = DetermineManifestPath(directoryPath);

            try
            {
                if (File.Exists(manifestPath))
                {
                    string[] songsDownloaded = File.ReadAllLines(manifestPath);

                    IList<string> newManifest = new List<string>();

                    foreach (string songDownloaded in songsDownloaded)
                    {
                        string localPath = ParseTrackPath(songDownloaded);

                        if (!allTracks.Select(song => song.LocalPath).Contains(localPath))
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
                throw new Exception("Unable to read manifest to determine tracks to delete");
            }
            
        }

        private IList<Track> DetermineTracksToDownload(string directoryPath, IList<Track> allSongs)
        {

            allSongs= allSongs.Select(c => { c.LocalPath = Path.Combine(directoryPath,c.Sanitize(c.Title)); return c; }).ToList();

            string manifestPath = DetermineManifestPath(directoryPath);

            IList<string> streamUrls = new List<string>();

            if (File.Exists(manifestPath))
            {
                foreach (string track in File.ReadAllLines(manifestPath))
                {
                    streamUrls.Add(ParseTrackPath(track));
                }
            }

            return allSongs.Where(s => !streamUrls.Contains(s.EffectiveDownloadUrl)).ToList();
        }

        

       

      

        private string RetrieveJson(string url, string clientId = null)
        {
            
            string json = null;
            
            try
            {
                using (WebClient client = new WebClient()) 
                {
                    json = client.DownloadString(url + (string.IsNullOrWhiteSpace(clientId) ? "" : ("?client_id=" + clientId)));
                }
             
            }
            catch (Exception)
            {
                // Nothing to do here
            }

            return json;
        }

        private string DetermineManifestPath(string directoryPath)
        {
            return Path.Combine(directoryPath, "manifest");
        }

        private string ParseTrackPath(string csv)
        {
            return csv != null && csv.IndexOf(',') >= 0 ? csv.Split(',')[0] : csv;
        }


    }

   
}
