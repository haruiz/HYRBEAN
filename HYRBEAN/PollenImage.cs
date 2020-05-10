using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.IO;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XImgproc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace HYRBEAN
{
    public abstract class DisposableImage : IDisposable
    {
        public Image<Bgr, Byte> rgb { get; set; }
        public Size preferredSize { get; set; }
        public Image<Gray, Byte> mask { get; set; }        
        public Dictionary<String, Bitmap> images;
        public String path { set; get; }
        public abstract Task extractBackground();
        public abstract Task extractContourns();
        public abstract Task process();
        public void Dispose(){
            if (rgb != null) rgb.Dispose();
            if (mask != null) mask.Dispose();
            images = null;
        }
    }

    public class PollenImage : DisposableImage {

        public List<PollenGrain> pollenGrains;
        

        public PollenImage(String imagePath, int w = 1024, int h = 780){
            try{
                this.path = imagePath;
                this.rgb = new Image<Bgr, byte>(imagePath);
                this.pollenGrains = new List<PollenGrain>();
                this.preferredSize = new Size(w, h);
            }
            catch (Exception ex){
                throw new Exception(String.Format("Error reading the file {0} : {1}", Path.GetFileNameWithoutExtension(imagePath), ex));
            }
        }

        private Task createMask(Image<Gray, Byte> src, int areaTreshold = 350)
        {
            #region fillGranes
            return Task.Run(delegate () {
                 VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                Mat hierarchy = new Mat();
                CvInvoke.FindContours(src.Clone(), contours, hierarchy, Emgu.CV.CvEnum.RetrType.External, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
                int contoursCount = contours.Size;
                for (int i = 0; i < contoursCount; i++){
                    using (VectorOfPoint contour = contours[i]){
                        double area = CvInvoke.ContourArea(contours[i], false);                        
                        if (area > areaTreshold){
                            CvInvoke.DrawContours(this.mask, contours, i, new MCvScalar(255), -1);                            
                        }
                        else{
                            CvInvoke.DrawContours(this.mask, contours, i, new MCvScalar(0), -1);
                        }
                    }
                }
                Mat kernel = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Ellipse, new Size(3, 3), new Point(-1, -1));
                this.mask._MorphologyEx(Emgu.CV.CvEnum.MorphOp.Erode, kernel, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(1.0));
                contours.Dispose();                
            });
            #endregion
        }

        public override async Task extractBackground(){
            Image<Gray, Byte> gray = rgb.Convert<Gray, Byte>();            
            Image<Gray, Byte> canny = gray.CopyBlank();
            this.mask = gray.CopyBlank();
            // borders detection
            Image<Gray, float> sobX = gray.Sobel(1, 0, 3);
            Image<Gray, float> sobY = gray.Sobel(0, 1, 3);
            sobX = sobX.AbsDiff(new Gray(0));
            sobY = sobY.AbsDiff(new Gray(0));
            Image<Gray, float> borders = sobX + sobY;
            gray = borders.Convert<Gray, Byte>();
            //canny filter
            CvInvoke.Canny(gray, canny, 20, 200);
            Mat kernel = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Ellipse, new Size(3, 3), new Point(-1, -1));
            canny._MorphologyEx(Emgu.CV.CvEnum.MorphOp.Close, kernel, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar(1.0));
            await createMask(canny);
            gray.Dispose();            
            borders.Dispose();
            canny.Dispose();
            sobX.Dispose();
            sobY.Dispose();            
        }

        public override Task extractContourns(){
            return Task.Run(delegate (){                
                this.rgb = this.rgb.Resize(this.preferredSize.Width, this.preferredSize.Height, Emgu.CV.CvEnum.Inter.Linear, true);
                this.mask = this.mask.Resize(this.preferredSize.Width, this.preferredSize.Height, Emgu.CV.CvEnum.Inter.Linear, true);
                Bitmap maskAsBitmap = mask.ToBitmap();
                Bitmap rgbAsBitmap = new Bitmap(this.rgb.ToBitmap());
                var dt = new BinaryWatershed(0.5f, DistanceTransformMethod.Euclidean);
                Bitmap output = dt.Apply(maskAsBitmap);
                BlobCounter bc = new BlobCounter();                
                bc.ObjectsOrder = ObjectsOrder.Area;
                bc.ProcessImage(output);
                Blob[] blobs = bc.GetObjectsInformation();
                foreach (Blob blob in blobs){
                    if (blob.Area > 100){
                        List<Accord.IntPoint> border = bc.GetBlobsEdgePoints(blob);
                        Point[] borderPoints = border.Select(pt => new Point(pt.X, pt.Y)).ToArray();
                        bc.ExtractBlobsImage(rgbAsBitmap, blob, false);
                        //this.rgb.Draw(new Cross2DF(new PointF(blob.CenterOfGravity.X, blob.CenterOfGravity.Y), 15, 15), new Bgr(0, 255, 0), 3);
                        this.pollenGrains.Add(new PollenGrain(blob));
                    }                    
                }

            });
         }

        public override async Task process()  {
            await this.extractBackground();
            await this.extractContourns();            
        }

        public Task exportRois(String outputFolder = null){
            if (outputFolder == null){
                String outputFolderName = Path.GetFileNameWithoutExtension(this.path);
                outputFolder = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "output", outputFolderName);
                if (Directory.Exists(outputFolder)){
                    Directory.Delete(outputFolder, true);
                }
                Directory.CreateDirectory(outputFolder);
            }
            return Task.Run(delegate (){
                foreach(var pollenGrain in this.pollenGrains){
                    String outputFile = $@"{outputFolder}/{Guid.NewGuid().ToString()}.jpg";
                    pollenGrain.blob.Image.ToManagedImage().Save(outputFile);
                }
            });
        }
    }
}
