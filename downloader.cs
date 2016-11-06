using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Xml;
using System.Globalization;
using iSpyApplication.Utilities;

namespace iSpyApplication
{
    public partial class downloader : Form
    {
        public string Url;
        public string SaveLocation;
        public bool WithBackup = true;
//        public string Format;

        private bool success;
        private bool aborting;
        //private bool cancel;

        public downloader()
        {
            InitializeComponent();
        }

        private void downloader_Load(object sender, EventArgs e)
        {
            UISync.Init(this);
            backgroundWorker1.RunWorkerAsync();
            Text = LocRm.GetString("Updating");

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string sUrlToReadFileFrom = Url;
            var url = new Uri(sUrlToReadFileFrom);
            var request = WebRequest.Create(url);
            var response = request.GetResponse();
            // gets the size of the file in bytes

            string backupLocation = null;
            long iSize = response.ContentLength;
            success = false;

            // use the webclient object to download the file

            using (Stream streamRemote = response.GetResponseStream())
            {
                if (streamRemote == null)
                {
                    Logger.LogErrorToFile("Response stream from " + Url + " failed");
                }
                else try
                {
                    streamRemote.ReadTimeout = 8000;
                    // read the stream in "byteBuffer" chunks and load the complete file in a memory stream
                    var byteBuffer = new byte[(int)Math.Min(64 * 1024, iSize)];
                    var ms = new MemoryStream((int)Math.Min(int.MaxValue, iSize));
                    while (ms.Position < iSize && !backgroundWorker1.CancellationPending)
                    {
                        int iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length);
                        if (iByteSize <= 0 || backgroundWorker1.CancellationPending)
                            break;
                        ms.Write(byteBuffer, 0, iByteSize);

                        var total = ms.Position;
                        // calculate the progress out of a base "100"
                        var dProgressPercentage = (double)total / (double)iSize;
                        var iProgressPercentage = (int) (dProgressPercentage*100);

                        // update the progress bar
                        backgroundWorker1.ReportProgress(iProgressPercentage);
                        UISync.Execute(() => lblProgress.Text = total + " / " + iSize + " [" + iProgressPercentage + " %]");
                    }
                    if (!backgroundWorker1.CancellationPending)
                    {
                        if (SaveLocation.EndsWith(".xml"))
                        {
                            ms.Position = 0;
                            var doc = new XmlDocument();
                            doc.Load(ms);
                            // Only check if xml parser succeeds but save the originally formatted file instead.
                            // doc.Save(SaveLocation);  // may reformat a little which makes it hard to detect changes.
                        }

                        if (WithBackup && File.Exists(SaveLocation))
                        {
                            var tryBackupLocation = SaveLocation
                                        + DateTime.Now.ToString(@"\#yyyy\-MM\-dd HH\-mm\-ss", CultureInfo.InvariantCulture);
                            File.Move(SaveLocation, tryBackupLocation);
                            backupLocation = tryBackupLocation;
                        }

                        ms.Position = 0;
                        var file = new FileStream(SaveLocation, FileMode.Create, FileAccess.Write);
                        ms.WriteTo(file);
                        file.Close();
                        success = true;
                        Logger.LogMessageToFile("Downloaded file from " + Url + " to " + SaveLocation);
                    }
                    else
                    {
                        Logger.LogMessageToFile("Update cancelled");
                    }
                    ms.Dispose();
                }
                catch (Exception ex)
                {
                    success = false;
                    Logger.LogExceptionToFile(ex, "Downloading file from " + Url + " to " + SaveLocation);
                    DialogResult = DialogResult.Cancel;
                    aborting = true;
                }
            }
            response.Close();
            if (backupLocation != null && File.Exists(backupLocation))
            {
                if (success)
                {
                    Logger.LogMessageToFile("Saved old " + SaveLocation + " as " + backupLocation);
                }
                else
                {
                    try
                    {
                        File.Delete(SaveLocation);
                    }
                    catch
                    {
                    }
                    try
                    {
                        File.Move(backupLocation, SaveLocation);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogExceptionToFile(ex, "Failed to restore " + SaveLocation + " from " + backupLocation) ;
                    }
                }
            }

        }

        private class UISync
        {
            private static ISynchronizeInvoke _sync;

            public static void Init(ISynchronizeInvoke sync)
            {
                _sync = sync;
            }

            public static void Execute(Action action)
            {
                try { _sync.BeginInvoke(action, null); }
                catch { }
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbDownloading.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (success)
            {
                DialogResult = DialogResult.OK;
            }
            Close();
        }

        private void downloader_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (backgroundWorker1.IsBusy && !aborting)
            {
                if (MessageBox.Show(this, LocRm.GetString("CancelUpdate"),LocRm.GetString("Confirm"), MessageBoxButtons.YesNo)==DialogResult.Yes)
                {
                    backgroundWorker1.CancelAsync();
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
