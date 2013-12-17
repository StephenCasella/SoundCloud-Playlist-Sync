SoundCloud-Playlist-Sync
========================

A utility to synchronize local directories with SoundCloud playlists.

After looking around and not finding any reasonable mechanism for downloading my SoundCloud playlists to local folders and keeping those folders in sync with the online playlist contents, I decided to build one. Here are the features:

* Multi-threaded downloading of all songs within any playlist
* Detection and downloading of high-quality song downloads when present
* Synchronization of a local directory with a playlist based on a manifest file (i.e. only downloads the songs that have not yet been downloaded and optionally deletes songs that have been downloaded but removed from the playlist)
* Meta-data tagging of downloaded files with author, track name, etc as specified by SoundCloud's API
* Some degree of automatic retries for downloads that fail due to connection timeouts or other reasons

I may continue to add features and improvements to this tool, like making it a service that automatically runs on login and silently keeps directories in sync with playlists or a reverse playlist id lookup, but a fully functional version is included in this post.

First, download the latest release zip file, extract it, and run the setup.exe file (if you have any older versions of this tool installed, uninstall them first). If you don't have Microsoft's .NET framework installed, the installation wizard will guide you through installing it as it is required by the download tool in order to operate.

Once the installation has completed, you will be greeted with a window like the following:

![screenshot](http://3.bp.blogspot.com/-uI-VGcD0G7M/UoArMSgi_fI/AAAAAAAABrY/bGjWTh1dIHA/s800/Screenshot+2013-11-10+19.55.23.png)

The Playlist URL it is asking for is NOT the one you see in your web browser when you view the playlist. It is in fact the URL that contains the playlist ID, which can be found by looking at the Share options for the playlist:

![screenshot](http://4.bp.blogspot.com/-AiWM3-2t_pQ/UoA2j_c23RI/AAAAAAAABsQ/4nyFVnPKD2Q/s800/Screenshot+from+2013-11-10+19:58:50.png)


The WordPress Code box shows the actual playlist URL containing the playlist ID (i.e. https://api.soundcloud.com/playlists/[some id]). This is the URL the application requires. Keep in mind the application will remember the information you enter, so you will only need to find this playlist URL once.

Second, you will need a SoundCloud API key. You can register one for free here: http://soundcloud.com/you/apps/new. This is the key that will allow the application to interact with SoundCloud's API in order to retrieve playlists and download songs. Give the new application any name you wish and then grab the Client ID it gives you on the next page, making sure to fully save the newly created application. Place that Client ID in the API Key field.

Third, you need to specify the directory you want the application to download your songs into. The songs will automatically be organized by artist like you see below:

![screenshot](http://2.bp.blogspot.com/-EQL9RCVCHpU/UoAtYj7HQtI/AAAAAAAABrs/MkZk9au00ZE/s640/Screenshot+2013-11-10+16.04.16.png)

As a side note, remember to not delete the manifest file the application creates. It is used to determine what songs need to be downloaded or removed in order to keep the directory in sync with the playlist.

Lastly, indicate whether you want songs that have already been downloaded but removed from the playlist to be deleted or left alone in your local folder and click the Synchronize button.

![screenshot](http://4.bp.blogspot.com/-5TwftlMvduA/UoAvFLfx2VI/AAAAAAAABsA/7zCw4r1a8gg/s1600/Screenshot+2013-11-10+16.12.49.png)


Depending on the size of the playlist, the application may run for a little while. You can always cancel the download and resume from where you left off (assuming you don't force kill the application). The first time it synchronizes, it will of course download all songs. The next times it runs, it will only download the songs it has not yet downloaded.

If you receive any download error messages, just run it again. It retry the songs that failed to download and succeed. At times files will fail to download due to connection issues, SoundCloud API throttling, or other reasons, but rerunning the sync will succeed.

At risk of stating the obvious, this is my own creation and is not affiliated with SoundCloud in any way. Also, the files that are downloaded are the ones that are originally streamed by SoundCloud (usually low quality versions) except when the uploader has included a high quality download link. Observe the copyright/etc laws of your country.


Gratuity is appreciated: 1PjsMvpVDxAEfekwVL6mEHyfunLm29Buer
