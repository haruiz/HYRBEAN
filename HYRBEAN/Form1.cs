using Accord;
using Emgu.CV;
using Emgu.CV.ML;
using Emgu.CV.Structure;
using MetroFramework.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HYRBEAN
{
    public partial class Form1 : MetroForm
    {
        public static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPE", ".BMP", ".GIF", ".PNG" };
        private Dictionary<String, PollenImage> images;
        private readonly SynchronizationContext synchronizationContext;
        private SVM model;
        public Form1()
        {
            InitializeComponent();
            string pathModel = Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "model.xml");
            if (File.Exists(pathModel)){
                Console.WriteLine("[INFO] Loading model");
                this.model = new SVM();
                using (FileStorage f = new FileStorage(pathModel, FileStorage.Mode.Read)){
                   this.model.Read(f.GetRoot());
                }
                Console.WriteLine("[INFO] model downloaded");
            }
            this.synchronizationContext = SynchronizationContext.Current;
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            try
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dialog.ShowDialog() == DialogResult.OK){
                    var files = Directory.EnumerateFiles(dialog.SelectedPath);
                    txtImagesDirectory.Text = dialog.SelectedPath;
                    files = files.Where(f => ImageExtensions.Contains(Path.GetExtension(f.ToUpper())));
                    if (files.Count() > 0){
                        this.flowLayoutImages.Controls.Clear();
                        this.readImages(files.ToArray());                        
                        this.btnExportRois.Enabled = true;
                    }
                    else{
                        MessageBox.Show("Images not found", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex){
                MessageBox.Show(ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async void readImages(string[] files)
        {
            this.images = new Dictionary<string, PollenImage>();            
            foreach(var file in files){
                PollenImage img = new PollenImage(file);
                this.images.Add(Path.GetFileNameWithoutExtension(file), img);
                await img.process();
                foreach (PollenGrain pollenGrain in img.pollenGrains){
                    await pollenGrain.computeFeatures();
                    float label = this.model.Predict(pollenGrain.getFeaturesAsMatrix());                    
                    pollenGrain.label = Convert.ToInt32(label) == 1 ? PollenType.viable : PollenType.nonViable;
                    if(pollenGrain.label == PollenType.viable){
                        //img.rgb.Draw(new Cross2DF(new PointF(pollenGrain.blob.CenterOfGravity.X, pollenGrain.blob.CenterOfGravity.Y), 15, 15), new Bgr(0, 255, 0), 1);
                        img.rgb.Draw(pollenGrain.blob.Rectangle, new Bgr(0, 255, 0), 2);
                    }
                    else
                        //img.rgb.Draw(new Cross2DF(new PointF(pollenGrain.blob.CenterOfGravity.X, pollenGrain.blob.CenterOfGravity.Y), 15, 15), new Bgr(255, 0, 255), 2);
                        img.rgb.Draw(pollenGrain.blob.Rectangle, new Bgr(255, 0, 255), 2);
                }
                Dictionary<String, Bitmap> outputImages = new Dictionary<string, Bitmap>();
                outputImages.Add("Rgb", img.rgb.Resize(800, 600, Emgu.CV.CvEnum.Inter.Linear, true).ToBitmap());
                outputImages.Add("Mask", img.mask.Resize(800, 600, Emgu.CV.CvEnum.Inter.Linear, true).ToBitmap());
                this.addImage(file,outputImages);
            }   
            
        }


        private void addImage(String pathImage, Dictionary<String, Bitmap> outputImages){
            #region addImage
            Panel pnlLayout = new Panel();
            pnlLayout.Size = new Size(180, 220);            
            //tab control
            TabControl tabControl = new TabControl();
            tabControl.Size = new Size(180, 180);
            tabControl.Dock = DockStyle.Top;
            tabControl.Appearance = TabAppearance.Normal;
            tabControl.Cursor = Cursors.Hand;
            // add tabs dynamicly 
            foreach (var entry in outputImages)
            {
                TabPage tabPage = new TabPage();
                tabPage.Text = entry.Key;
                PictureBox pbx = new PictureBox();
                pbx.Size = new Size(150, 150);                
                pbx.Margin = new Padding(10);
                pbx.Cursor = Cursors.Hand;
                pbx.SizeMode = PictureBoxSizeMode.Zoom;
                pbx.Dock = DockStyle.Fill;
                pbx.DoubleClick += Pbx_DoubleClick;
                pbx.Image = entry.Value;
                tabPage.Controls.Add(pbx);
                tabControl.TabPages.Add(tabPage);
            }
            // info panel
            Panel pnlInfo = new Panel();
            pnlInfo.Size = new Size(180, 60);
            pnlInfo.Dock = DockStyle.Bottom;
            Label lblFileName = new Label();
            lblFileName.Text = Path.GetFileName(pathImage);
            lblFileName.TextAlign = ContentAlignment.MiddleCenter;
            lblFileName.Dock = DockStyle.Fill;
            pnlInfo.Controls.Add(lblFileName);
            pnlLayout.Controls.Add(pnlInfo);
            pnlLayout.Controls.Add(tabControl);
            this.flowLayoutImages.Controls.Add(pnlLayout); 
            #endregion
        }

        private void Pbx_DoubleClick(object sender, EventArgs e){
            PictureBox pbox = sender as PictureBox;
            Image<Bgr, Byte> imageClicked = (pbox.Image as Bitmap).ToImage<Bgr, Byte>();
            Emgu.CV.UI.ImageViewer viewer = new Emgu.CV.UI.ImageViewer(imageClicked);
            viewer.StartPosition = FormStartPosition.CenterScreen;
            viewer.ShowDialog();
        }


        private void btnExportRois_Click(object sender, EventArgs e)
        {
            Task.WhenAll(this.images.Values.Select(img => img.export()));
            MessageBox.Show("Pollen grains ROIS exported successfully!");
        }

    }




}
