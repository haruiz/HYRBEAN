using Accord.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HYRBEAN
{
    public enum PollenType { viable = 1, nonViable = 2, intermedium = 3, unknown = 4 }
    public class PollenGrain
    {
        public int width { get; set; }
        public int height { get; set; }
        public float centroideY;
        public double area { get; set; }
        public Guid id { get; set; }
        public double eccentricity { get; set; }
        public int npoints { get; set; }
        public Blob blob { get; set; }
        public double shape { get; set; }
        public double perimeter { get; set; }
        public PollenType label { get; set; }
        public float centroideX { get; internal set; }
        public float radius { get; internal set; }
        public float aspectRatio { get; internal set; }
        public double extent { get; internal set; }
        public MCvScalar hsvColor { get; internal set; }
        public MCvScalar bgrColor { get; internal set; }        
        public double homogeneity { get; internal set; }
        public double contrast { get; internal set; }
        public double[] haralickFeatures { get; internal set; }


        public PollenGrain(Blob blob)
        {
            this.blob = blob;
            this.label = PollenType.unknown;
            this.id = Guid.NewGuid();
        }

        internal Task computeFeatures()
        {
            return Task.Run(delegate () {
                using (Image<Bgr, Byte> src = this.blob.Image.ToManagedImage().ToImage<Bgr, Byte>()){
                    VectorOfPoint contour = FeaturesUtilities.getMaxContour(src);
                    if (contour != null && contour.Size > 5){
                        CircleF circle = CvInvoke.MinEnclosingCircle(contour);
                        RotatedRect ellipse = CvInvoke.FitEllipse(contour);
                        // extract shape features
                        Rectangle bounds = CvInvoke.BoundingRectangle(contour);
                        this.area = CvInvoke.ContourArea(contour, false);
                        this.perimeter = CvInvoke.ArcLength(contour, true);
                        this.shape = (4 * Math.PI * area) / Math.Pow(perimeter, 2);
                        this.npoints = contour.Size;
                        this.width = bounds.Width;
                        this.height = bounds.Height;
                        this.centroideY = circle.Center.Y;
                        this.centroideX = circle.Center.X;
                        this.radius = circle.Radius;
                        this.aspectRatio = width / height;
                        this.extent = area / (width * height);

                        //extract texture features
                        using (Image<Gray, Byte> gray = src.Convert<Gray, Byte>()){
                            var glcm = new GrayLevelCooccurrenceMatrix(distance: 1, degree: CooccurrenceDegree.Degree0, normalize: true);
                            // Extract the matrix from the image
                            double[,] matrix = glcm.Compute(gray.ToBitmap());
                            HaralickDescriptor haralick = new HaralickDescriptor(matrix);
                            double[] haralickFeatures = haralick.GetVector();
                            this.homogeneity = haralick.AngularSecondMomentum;
                            this.contrast = haralick.Contrast;
                            this.haralickFeatures = haralickFeatures;
                        }

                        using (Image<Hsv, Byte> hsv = src.Convert<Hsv, Byte>()) {
                            this.hsvColor = new MCvScalar(){
                                V0 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 0],
                                V1 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 1],
                                V2 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 2]
                            };
                        }

                        this.bgrColor = new MCvScalar(){
                            V0 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 0],
                            V1 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 1],
                            V2 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 2]
                        };
                    }
                }
            });
        }

        internal Dictionary<string, string> getFeaturesAsDict(){
            Dictionary<String, String> features = new Dictionary<string, string>();
            features.Add("id", this.id.ToString());
            features.Add("width", this.width.ToString());
            features.Add("height", this.height.ToString());
            features.Add("homogeneity", String.Format("{0:0.##}", this.homogeneity));
            features.Add("contrast", String.Format("{0:0.##}", this.contrast));
            features.Add("perimeter", String.Format("{0:0.##}", this.perimeter));
            features.Add("shape", String.Format("{0:0.##}", this.shape));
            features.Add("aspectRatio", String.Format("{0:0.##}", this.aspectRatio));
            features.Add("area", String.Format("{0:0.##}", this.area));
            features.Add("centroideX", String.Format("{0:0.##}", this.centroideX));
            features.Add("centroideY", String.Format("{0:0.##}", this.centroideY));
            features.Add("extent", String.Format("{0:0.##}", this.extent));
            features.Add("radius", String.Format("{0:0.##}", this.radius));
            features.Add("avgBgrB", this.bgrColor.V0.ToString());
            features.Add("avgBgrG", this.bgrColor.V1.ToString());
            features.Add("avgBgrR", this.bgrColor.V2.ToString());
            features.Add("avgHsvH", this.hsvColor.V0.ToString());
            features.Add("avgHsvS", this.hsvColor.V1.ToString());
            features.Add("avgHsvV", this.hsvColor.V2.ToString());
            features.Add("npoints", this.npoints.ToString());
            features.Add("class", String.Format("{0}", (int)this.label));
            return features;
        }

        internal Matrix<float> getFeaturesAsMatrix(){
            Dictionary<string, string> featuresDict = this.getFeaturesAsDict();
            float[] featuresVector = featuresDict.Where(f => f.Key != "id" && f.Key != "class").Select(f => Convert.ToSingle(f.Value)).ToArray();
            //Console.WriteLine(String.Join(",", featuresVector));
            Matrix<float> row = FeaturesUtilities.vector2matrix(featuresVector);
            return row;
        }


        internal float[] getFeaturesAsVector(){
            Dictionary<string, string> featuresDict = this.getFeaturesAsDict();
            float[] featuresVector = featuresDict.Where(f => f.Key != "id" && f.Key != "class").Select(f => Convert.ToSingle(f.Value)).ToArray();
            return featuresVector;
        }

    }
   }
