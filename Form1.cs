
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;

namespace RevitTranslator
{
    public partial class Form1 : Form
    {
        private const int SERVER_PORT = 3000;

        public Form1()
        {
            
            InitializeComponent();
        }

        SimpleHTTPServer viewerServer;

        private void Form1_Load(object sender, EventArgs e)
        {
            var serverFolder = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Viewer");
            viewerServer = new SimpleHTTPServer(serverFolder, SERVER_PORT);
        }

        private void EnableUI(bool enabled, string message, int progress = -1)
        {
            BeginInvoke((Action)(() =>
            {
                toolStripStatusLabel1.Text = message;
                groupBox1.Enabled = enabled;
                groupBox2.Enabled = enabled;

                if(progress < 0)
                {
                    toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
                }
                else
                {
                    toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
                    toolStripProgressBar1.Value = progress;
                }

            }));
        }

        private async void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ForgeApi.FORGE_CLIENT_ID = txtClientId.Text;
            ForgeApi.FORGE_SECRET = txtSecret.Text;

            if(string.IsNullOrEmpty(ForgeApi.FORGE_CLIENT_ID) || string.IsNullOrEmpty(ForgeApi.FORGE_SECRET))
            {
                MessageBox.Show("Please fill in the Client ID and Secret fields correctly", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Revit File | *.rvt";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                EnableUI(false, "Uploading to Forge...");

                var urn = await ForgeApi.UploadRevitFile(fileDialog.FileName);

                BeginInvoke((Action)(() =>
                {
                    txtUrn.Text = urn;
                    txtName.Text = Path.GetFileName(fileDialog.FileName);
                }
                ));

                var outputFolder = Path.Combine(Path.GetDirectoryName(fileDialog.FileName), urn);

                Directory.CreateDirectory(outputFolder);

                BeginInvoke((Action)(() =>
                   toolStripStatusLabel1.Text = $"Translating..."
                ));

                var derivativeUrns = await ForgeApi.Translate(urn, (progress, manifest) =>
                {
                    BeginInvoke((Action)(() => {
                        txtLog.Text = manifest;
                    }));

                    EnableUI(false, $"Translating... ({progress}%)", progress);
                });

                EnableUI(false, $"Downloading files...", -1);

                foreach (var du in derivativeUrns)
                {
                    EnableUI(false, $"Downloading {du}", -1);
                    await ForgeApi.GetDerivativeManifest(derivativeUrns.First(), du, outputFolder);
                }

                var thumbnail = await ForgeApi.GetThumbnail(urn, outputFolder);

                BeginInvoke((Action)(() =>
                   pictureBox1.Image = Image.FromFile(thumbnail)
                ));

                var derivativeUrn = derivativeUrns.First();

                var metadata = await ForgeApi.GetMetadata(derivativeUrn);

                BeginInvoke((Action)(() =>
                     txtLog.AppendText("\r\n\r\n" + metadata)
                ));

                EnableUI(true, "Completed", 0);

                var url = await ForgeApi.GetViewerUrl(SERVER_PORT, derivativeUrn);

                BeginInvoke((Action)(() =>
                {
                    chromiumWebBrowser1.Load(url);
                    MessageBox.Show($"Output files were saved to {outputFolder}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Process.Start(outputFolder);
                    txtUrn.Text = derivativeUrn;
                }
                ));

            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            viewerServer.Stop();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var urn = txtUrn.Text;
            if(!string.IsNullOrEmpty(urn))
            {
                BeginInvoke((Action)(() =>
                     btnCopyToClipboard.Enabled = false
                ));

                var url = await ForgeApi.GetViewerUrl(SERVER_PORT, urn);
                Clipboard.SetText(url);

                BeginInvoke((Action)(() =>
                     btnCopyToClipboard.Enabled = true
                ));
            }
            else
            {
                MessageBox.Show($"Please open a file first!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

}
