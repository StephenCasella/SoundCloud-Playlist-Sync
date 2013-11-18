namespace Soundcloud_Playlist_Downloader
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.syncButton = new System.Windows.Forms.Button();
            this.browseButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.deleteRemovedSongs = new System.Windows.Forms.CheckBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.status = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.directoryPath = new System.Windows.Forms.TextBox();
            this.apiKey = new System.Windows.Forms.TextBox();
            this.playlistUrl = new System.Windows.Forms.TextBox();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(26, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Playlist URL";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(45, 42);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(45, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "API Key";
            // 
            // syncButton
            // 
            this.syncButton.Location = new System.Drawing.Point(15, 116);
            this.syncButton.Name = "syncButton";
            this.syncButton.Size = new System.Drawing.Size(390, 23);
            this.syncButton.TabIndex = 4;
            this.syncButton.Text = "Synchronize";
            this.syncButton.UseVisualStyleBackColor = true;
            this.syncButton.Click += new System.EventHandler(this.syncButton_Click);
            // 
            // browseButton
            // 
            this.browseButton.Location = new System.Drawing.Point(353, 65);
            this.browseButton.Name = "browseButton";
            this.browseButton.Size = new System.Drawing.Size(52, 23);
            this.browseButton.TabIndex = 6;
            this.browseButton.Text = "Browse";
            this.browseButton.UseVisualStyleBackColor = true;
            this.browseButton.Click += new System.EventHandler(this.browseButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 70);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(78, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Local Directory";
            // 
            // deleteRemovedSongs
            // 
            this.deleteRemovedSongs.AutoSize = true;
            this.deleteRemovedSongs.Location = new System.Drawing.Point(96, 93);
            this.deleteRemovedSongs.Name = "deleteRemovedSongs";
            this.deleteRemovedSongs.Size = new System.Drawing.Size(239, 17);
            this.deleteRemovedSongs.TabIndex = 8;
            this.deleteRemovedSongs.Text = "Delete Removed Songs From Local Directory";
            this.deleteRemovedSongs.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 163);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(417, 22);
            this.statusStrip1.TabIndex = 9;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // status
            // 
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(0, 17);
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(15, 145);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(390, 13);
            this.progressBar.TabIndex = 10;
            // 
            // directoryPath
            // 
            this.directoryPath.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Soundcloud_Playlist_Downloader.Properties.Settings.Default, "LocalPath", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.directoryPath.Location = new System.Drawing.Point(96, 67);
            this.directoryPath.Name = "directoryPath";
            this.directoryPath.Size = new System.Drawing.Size(251, 20);
            this.directoryPath.TabIndex = 5;
            this.directoryPath.Text = global::Soundcloud_Playlist_Downloader.Properties.Settings.Default.LocalPath;
            // 
            // apiKey
            // 
            this.apiKey.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Soundcloud_Playlist_Downloader.Properties.Settings.Default, "ApiKey", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.apiKey.Location = new System.Drawing.Point(96, 39);
            this.apiKey.Name = "apiKey";
            this.apiKey.Size = new System.Drawing.Size(309, 20);
            this.apiKey.TabIndex = 3;
            this.apiKey.Text = global::Soundcloud_Playlist_Downloader.Properties.Settings.Default.ApiKey;
            // 
            // playlistUrl
            // 
            this.playlistUrl.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Soundcloud_Playlist_Downloader.Properties.Settings.Default, "PlaylistUrl", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.playlistUrl.Location = new System.Drawing.Point(96, 13);
            this.playlistUrl.Name = "playlistUrl";
            this.playlistUrl.Size = new System.Drawing.Size(309, 20);
            this.playlistUrl.TabIndex = 1;
            this.playlistUrl.Text = global::Soundcloud_Playlist_Downloader.Properties.Settings.Default.PlaylistUrl;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(417, 185);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.deleteRemovedSongs);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.browseButton);
            this.Controls.Add(this.directoryPath);
            this.Controls.Add(this.syncButton);
            this.Controls.Add(this.apiKey);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.playlistUrl);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "SoundCloud Playlist Sync";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox playlistUrl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox apiKey;
        private System.Windows.Forms.Button syncButton;
        private System.Windows.Forms.TextBox directoryPath;
        private System.Windows.Forms.Button browseButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox deleteRemovedSongs;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel status;
        private System.Windows.Forms.ProgressBar progressBar;
    }
}

