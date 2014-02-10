﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Declarations;
using Declarations.Events;
using Declarations.Media;
using Declarations.Players;
using Implementation;
using iSpyApplication.Controls;

namespace iSpyApplication
{
    public partial class PlayerVLC : Form
    {
        readonly IMediaPlayerFactory _mFactory;
        readonly IDiskPlayer _mPlayer;
        IMedia _mMedia;
        private readonly string _titleText;

        private bool _needsSize = true;
        public int ObjectID = -1;
        public MainForm MF;

        private readonly FolderSelectDialog _fbdSaveTo = new FolderSelectDialog
        {
            Title = "Select a folder to copy the file to"
        };

        private void RenderResources()
        {
            openFolderToolStripMenuItem.Text = LocRm.GetString("OpenLocalFolder");
            saveAsToolStripMenuItem.Text = LocRm.GetString("SaveAs");
        }

         public PlayerVLC(string titleText, MainForm mf)
         {
            MF = mf;
            InitializeComponent();

            _mFactory = new MediaPlayerFactory(true);
            _mPlayer = _mFactory.CreatePlayer<IDiskPlayer>();

            _mPlayer.Events.PlayerPositionChanged += EventsPlayerPositionChanged;
            _mPlayer.Events.TimeChanged += EventsTimeChanged;
            _mPlayer.Events.MediaEnded += EventsMediaEnded;
            _mPlayer.Events.PlayerStopped += EventsPlayerStopped;

            _mPlayer.WindowHandle = pnlMovie.Handle;
            
             if (_mPlayer.Volume>=trackBar2.Minimum && _mPlayer.Volume<=trackBar2.Maximum)
                trackBar2.Value = _mPlayer.Volume;
            
             RenderResources();
             _titleText = titleText;
             chkRepeatAll.Checked = MainForm.VLCRepeatAll;

         }

        private void Player_Load(object sender, EventArgs e)
        {
            UISync.Init(this);
            _mPlayer.MouseInputEnabled = true;
            vNav.Seek += vNav_Seek;
            Text = _titleText;
        }

        void vNav_Seek(object sender, float percent)
        {
            if (!_mPlayer.IsPlaying)
            {
                if (_mPlayer.PlayerWillPlay)
                    _mPlayer.Play();
                else
                    Play(_filename, Text);
            }

            _mPlayer.Position = percent / 100;
        }

        private string _filename = "";
        private object _lock = new object();

        private delegate void PlayDelegate(string filename, string titleText);
        public void Play(string filename, string titleText)
        {
            if (InvokeRequired)
                Invoke(new PlayDelegate(Play), filename, titleText);
            else
            {
                if (!File.Exists(filename))
                {
                    MessageBox.Show(this, "File could not be found:\n" + filename);
                    return;
                }
                _needsSize = _filename != filename;
                _filename = filename;
                lock (_lock)
                {
                    _mMedia = _mFactory.CreateMedia<IMediaFromFile>(filename);
                    _mMedia.Events.DurationChanged += EventsDurationChanged;
                    _mMedia.Events.StateChanged += EventsStateChanged;
                    _mMedia.Events.ParsedChanged += Events_ParsedChanged;
                    try
                    {
                        _mPlayer.Open(_mMedia);

                        _mMedia.Parse(false);
                    }
                    catch (Exception ex)
                    {
                        MainForm.LogExceptionToFile(ex);
                        MessageBox.Show(this, "Could not open this file:\n" + filename);
                        return;
                    }

                    _mPlayer.Play();
                }

                string[] parts = filename.Split('\\');
                string fn = parts[parts.Length - 1];
                FilesFile ff = null;
                if (fn.EndsWith(".mp3") || fn.EndsWith(".wav"))
                {
                    var vl = ((MainForm)Owner).GetVolumeLevel(ObjectID);
                    if (vl != null)
                    {
                        ff = vl.FileList.FirstOrDefault(p => p.Filename.EndsWith(fn));
                    }
                    vNav.IsAudio = true;
                    pnlMovie.BackgroundImage = Properties.Resources.ispy1audio;
                }
                else
                {
                    var cw = ((MainForm)Owner).GetCameraWindow(ObjectID);
                    if (cw!=null)   {
                        ff = cw.FileList.FirstOrDefault(p => p.Filename.EndsWith(fn));
                    }
                    vNav.IsAudio = false;
                    pnlMovie.BackgroundImage = Properties.Resources.ispy1;
                }
                
                if (ff!=null)
                    vNav.Init(ff);
                Text = titleText;
            }
        }
        void EventsPlayerStopped(object sender, EventArgs e)
        {
            UISync.Execute(InitControls);
        }

        void EventsMediaEnded(object sender, EventArgs e)
        {
            UISync.Execute(InitControls);
        }

        private void InitControls()
        {
            lblTime.Text = "00:00:00";
        }

        void EventsTimeChanged(object sender, MediaPlayerTimeChanged e)
        {
            UISync.Execute(() => lblTime.Text = TimeSpan.FromMilliseconds(e.NewTime).ToString().Substring(0, 8));
        }

        void EventsPlayerPositionChanged(object sender, MediaPlayerPositionChanged e)
        {
            var newpos = (int) (e.NewPosition*100);
            if (newpos<0)
                newpos = 0;
            if (newpos>100)
                newpos = 100;
            UISync.Execute(() => vNav.Value = newpos);
            if (_needsSize)
            {
                Size sz = _mPlayer.GetVideoSize(0);
                if (sz.Width > 0)
                {
                    if (sz.Width < 320)
                        sz.Width = 320;
                    if (sz.Height < 240)
                        sz.Height = 240;

                    if (Width != sz.Width)
                        UISync.Execute(() => Width = sz.Width);
                    if (Height != sz.Height + tableLayoutPanel1.Height)
                        UISync.Execute(() => Height = sz.Height + tableLayoutPanel1.Height);
                    _needsSize = false;
                }
            }
        }


        void EventsStateChanged(object sender, MediaStateChange e)
        {
            UISync.Execute(() => label1.Text = e.NewState.ToString());
            switch (e.NewState)
            {
                case MediaState.Playing:
                    UISync.Execute(() => btnPlayPause.Text = LocRm.GetString("||"));
                    break;
                case MediaState.Ended:
                    if (chkRepeatAll.Checked)
                        Go(1);

                    UISync.Execute(() => btnPlayPause.Text = LocRm.GetString(">"));
                    break;
                default:
                    UISync.Execute(() => btnPlayPause.Text = LocRm.GetString(">"));
                    break;
            }
        }

        void EventsDurationChanged(object sender, MediaDurationChange e)
        {
            //UISync.Execute(() => lblDuration.Text = TimeSpan.FromMilliseconds(e.NewDuration).ToString().Substring(0, 8));
        }


        void Events_ParsedChanged(object sender, MediaParseChange e)
        {
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            try
            {
                _mPlayer.Volume = trackBar2.Value;
            }
            catch{}
        }

        private void button4_Click(object sender, EventArgs e)
        {
            _mPlayer.Stop();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_mPlayer.IsPlaying)
            {
                _mPlayer.Pause();
            }
            else
            {
                if (_mPlayer.PlayerWillPlay)
                    _mPlayer.Play();
                else
                    Play(_filename, Text);
            }
        }

        private class UISync
        {
            private static ISynchronizeInvoke Sync;

            public static void Init(ISynchronizeInvoke sync)
            {
                Sync = sync;
            }

            public static void Execute(Action action)
            {
                try {Sync.BeginInvoke(action, null);}
                catch{}
            }
        }

        private void Player_FormClosing(object sender, FormClosingEventArgs e)
        {
            lock (_lock)
            {
                if (_mPlayer != null)
                {
                    
                    try
                    {
                        _mPlayer.Stop();
                    }
                    catch
                    {
                    }
                }
                try
                {
                    if (_mFactory != null)
                        _mFactory.Dispose();
                    if (_mMedia != null)
                        _mMedia.Dispose();
                    if (_mPlayer != null)
                        _mPlayer.Dispose();
                }
                catch
                {
                    
                }
            }
            if (vNav!=null)
                vNav.ReleaseGraph();
        }

        private void tbSpeed_Scroll(object sender, EventArgs e)
        {
            _mPlayer.PlaybackRate = ((float) tbSpeed.Value)/10;
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var fi = new FileInfo(_filename);

                if (_fbdSaveTo.ShowDialog(Handle))
                {
                    File.Copy(_filename, _fbdSaveTo.FileName + @"\" + fi.Name);
                }
            }
            catch (Exception ex)
            {
                MainForm.LogExceptionToFile(ex);
            }
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string argument = @"/select, " + _filename;
            Process.Start("explorer.exe", argument);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            Go(1);

        }

        private void Go(int n)
        {
            int j = 0;
            lock (MF.flowPreview.Controls)
            {
                var lb = (from Control c in MF.flowPreview.Controls select c as PreviewBox into pb where pb != null && pb.Selected select pb).ToList();

                for (int i = 0; i < lb.Count; i++)
                {
                    var pb = lb[i];
                    if (pb.FileName == _filename)
                    {
                        j = i + n;
                        if (lb.Count <= j)
                        {
                            //stop at the last movie
                            return;
                        }
                        if (j < 0)
                            j = lb.Count - 1;
                        break;
                    }
                }
                if (j > -1 && j<lb.Count)
                {
                    UISync.Execute(() => lb[j].PlayMedia(3));
                }
            }
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            Go(-1);
        }

        private void chkRepeatAll_CheckedChanged(object sender, EventArgs e)
        {
            MainForm.VLCRepeatAll = chkRepeatAll.Checked;
        }
    }
}
