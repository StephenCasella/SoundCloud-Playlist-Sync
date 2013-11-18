using Soundcloud_Playlist_Downloader.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Soundcloud_Playlist_Downloader
{
    public partial class Form1 : Form
    {
        private PlaylistSync sync = null;
        private delegate void ProgressBarUpdate();
        private delegate void PerformSyncComplete();
        private delegate void PerformStatusUpdate();

        private bool completed = false;

        private PerformSyncComplete PerformSyncCompleteImplementation = null;
        private ProgressBarUpdate ProgressBarUpdateImplementation = null;
        private PerformStatusUpdate PerformStatusUpdateImplementation = null;

        private string DefaultActionText = "Synchronize";
        private string AbortActionText = "Abort";

        private bool exiting = false;

        public Form1()
        {
            InitializeComponent();
            sync = new PlaylistSync();
            PerformSyncCompleteImplementation = SyncCompleteButton;
            ProgressBarUpdateImplementation = UpdateProgressBar;
            PerformStatusUpdateImplementation = UpdateStatus;
            status.Text = "Ready";
            MinimumSize = new Size(Width, Height);
            MaximumSize = new Size(Width, Height);
            
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                directoryPath.Text = dialog.SelectedPath;
            }
        }

        private void UpdateStatus()
        {
            if (!exiting)
            {
                if (sync.IsActive && progressBar.Value == progressBar.Maximum && progressBar.Value != progressBar.Minimum)
                {
                    status.Text = "Completed";
                }
                else if (sync.IsActive && progressBar.Value >= progressBar.Minimum && progressBar.Maximum > 0)
                {
                    status.Text = "Synchronizing... " + progressBar.Value + " of " + progressBar.Maximum + " songs downloaded.";
                }
                else if (sync.IsActive && completed)
                {
                    status.Text = "Playlist is already synchronized";
                }
                else if (!sync.IsActive && syncButton.Text == AbortActionText)
                {
                    status.Text = "Aborting downloads... Please Wait.";
                }
                else if (sync.IsActive)
                {
                    status.Text = "Synchronizing...";
                }
                else if (!sync.IsActive)
                {
                    status.Text = "Aborted";
                }
            }
            else if (completed)
            {
                // the form has indicated it is being closed and the sync utility has finished aborting
                Close();
                Dispose();
            }
            
        }

        private void InvokeUpdateStatus()
        {
            statusStrip1.Invoke(PerformStatusUpdateImplementation);
        }

        private void UpdateProgressBar()
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = sync.TotalSongsToDownload;
            progressBar.Value = sync.TotalSongsDownloaded;
        }

        private void InvokeUpdateProgressBar()
        {
            progressBar.Invoke(ProgressBarUpdateImplementation);
        }

        private void InvokeSyncComplete()
        {
            syncButton.Invoke(PerformSyncCompleteImplementation);
        }

        private void SyncCompleteButton()
        {
            syncButton.Text = DefaultActionText;
            syncButton.Enabled = true;
        }

        private void syncButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(playlistUrl.Text) &&
                !string.IsNullOrWhiteSpace(apiKey.Text) &&
                !string.IsNullOrWhiteSpace(directoryPath.Text) &&
                syncButton.Text == DefaultActionText)
            {
                syncButton.Text = AbortActionText;
                status.Text = "Checking for playlist changes...";
                completed = false;
                progressBar.Value = 0;
                progressBar.Maximum = 0;
                progressBar.Minimum = 0;
                new Thread(() =>
                {
                    try
                    {
                        sync.Synchronize(playlistUrl.Text, apiKey.Text,
                            directoryPath.Text, deleteRemovedSongs.Checked);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error");
                    }
                    finally
                    {
                        completed = true;
                        InvokeSyncComplete();
                    }
                }).Start();

                new Thread(() =>
                {
                    // perform progress updates
                    while (!completed && !exiting)
                    {
                        Thread.Sleep(500);
                        InvokeUpdateStatus();
                        InvokeUpdateProgressBar();
                    }
                    if (!exiting)
                    {
                        InvokeUpdateStatus();
                    }

                }).Start();

            }
            else if (sync.IsActive && syncButton.Text == AbortActionText)
            {
                sync.IsActive = false;
                syncButton.Enabled = false;
            }
            else if (syncButton.Text == DefaultActionText && 
                string.IsNullOrWhiteSpace(playlistUrl.Text))
            {
                status.Text = "Enter playlist url";
            }
            else if (syncButton.Text == DefaultActionText &&
                string.IsNullOrWhiteSpace(apiKey.Text))
            {
                status.Text = "Enter API key";
            }
            else if (syncButton.Text == DefaultActionText &&
                string.IsNullOrWhiteSpace(directoryPath.Text))
            {
                status.Text = "Enter local directory path";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Default.Save();
            exiting = true;
            sync.IsActive = false;
            status.Text = "Preparing for exit... Please Wait.";
            syncButton.Enabled = false;

            if (syncButton.Text != DefaultActionText)
            {
                e.Cancel = true;
            }
            else
            {
                syncButton.Text = AbortActionText;
            }
        }
    }
}
