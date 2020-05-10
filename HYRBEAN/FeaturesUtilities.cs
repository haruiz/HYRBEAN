using Accord.Imaging;
using Accord.IO;
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
    class FeaturesUtilities{       


        public static float[] histogramFeatures(Image<Bgr, byte> src, Image<Gray, byte> mask) {

            Image<Lab, Byte> dst = src.Convert<Lab, Byte>();
            DenseHistogram histo = new DenseHistogram(255, new RangeF(0, 255));
            Image<Gray, Byte>[] channels = dst.Split();
            histo.Calculate(new Image<Gray, Byte>[] { channels[0] }, true, mask);
            CvInvoke.Normalize(histo, histo);
            MatND<float> mat = new MatND<float>(255);
            histo.CopyTo(mat);
            float[] binValuesB = (float[])mat.ManagedArray;
            histo.Clear();
            histo.Calculate(new Image<Gray, Byte>[] { channels[1] }, true, mask);
            CvInvoke.Normalize(histo, histo);
            mat = new MatND<float>(255);
            histo.CopyTo(mat);
            float[] binValuesG = (float[])mat.ManagedArray;
            histo.Clear();
            histo.Calculate(new Image<Gray, Byte>[] { channels[2] }, true, mask);
            CvInvoke.Normalize(histo, histo);
            mat = new MatND<float>(255);
            histo.CopyTo(mat);
            float[] binValuesR = (float[])mat.ManagedArray;
            histo.Clear();
            return binValuesB.Concat(binValuesG).Concat(binValuesR).ToArray();

        }

        public static float[] partsFeatures(Image<Bgr, byte> src)
        {
            Point center = new Point((int)(src.Width * 0.5), (int)(src.Height * 0.5));
            Rectangle[] segments = new Rectangle[] {
                new Rectangle(0, 0, center.X, center.Y),
                new Rectangle(center.X, 0, src.Width, center.Y),
                new Rectangle(center.X, center.Y, src.Width, src.Height),
                new Rectangle(0, center.Y, center.X, src.Height),
            };
            Image<Gray, Byte> ellipseMask = new Image<Gray, Byte>(src.Size);
            Size axes = new Size((int)(src.Width * 0.75) / 2, (int)(src.Height * 0.75) / 2);
            CvInvoke.Ellipse(ellipseMask, center, axes, 0, 0, 360, new MCvScalar(255), -1);
            List<float> features = new List<float>();
            foreach (Rectangle s in segments)
            {
                Image<Gray, Byte> cornerMask = new Image<Gray, byte>(src.Size);
                CvInvoke.Rectangle(cornerMask, s, new MCvScalar(255), -1);
                CvInvoke.Subtract(cornerMask, ellipseMask, cornerMask);
                features.Add(Convert.ToSingle(src.GetAverage(cornerMask).Red));
                features.Add(Convert.ToSingle(src.GetAverage(cornerMask).Green));
                features.Add(Convert.ToSingle(src.GetAverage(cornerMask).Blue));
                cornerMask.Dispose();
            }
            features.Add(Convert.ToSingle(src.GetAverage(ellipseMask).Red));
            features.Add(Convert.ToSingle(src.GetAverage(ellipseMask).Green));
            features.Add(Convert.ToSingle(src.GetAverage(ellipseMask).Blue));
            return features.ToArray();

        }

        public static float[] imageFeatures(Image<Bgr, byte> src, int w = 20, int h = 20){
            Image<Bgr, float> copy = src.Convert<Bgr, float>();
            copy = copy.Resize(w, h, Emgu.CV.CvEnum.Inter.Linear, false);
            Mat[] channels = copy.Mat.Split();
            int n = copy.Rows * copy.Cols;
            float[] vectorOfFeature = new float[n * 3];
            for (int i = 0; i < channels.Length; i++){
                Mat c = channels[0].Reshape(1, n);
                MatND<float> temp = new MatND<float>(n);
                c.CopyTo(temp);
                float[] partition = (float[])temp.ManagedArray;
                partition.CopyTo(vectorOfFeature, i * n);
                c.Dispose();
                temp.Dispose();
            }
            channels = null;
            copy.Dispose();
            return vectorOfFeature;
        }


        public static VectorOfPoint getMaxContour(Image<Bgr, Byte> src){
            VectorOfPoint maxContour = null;
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Image<Gray, Byte> gray = src.Convert<Gray, Byte>();
            Image<Gray, Byte> mask = gray.CopyBlank();
            CvInvoke.Threshold(gray, mask, 0, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            CvInvoke.FindContours(mask.Clone(), contours, null, Emgu.CV.CvEnum.RetrType.External, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
            int contoursCount = contours.Size;
            if (contoursCount > 0){
                float maxArea = 0;
                for (int i = 0; i < contoursCount; i++){
                    using (VectorOfPoint contour = contours[i]){
                        float area = Convert.ToSingle(CvInvoke.ContourArea(contour, false));
                        if (area > maxArea){
                            maxContour = contour.DeepClone();
                            maxArea = area;
                        }
                    }
                }
            }
            mask.Dispose();
            gray.Dispose();
            return maxContour;
            }


            public static float[] shapeFeatures(Image<Bgr, Byte> src){
            List<float> vectorOfFeatures = new List<float>();
            VectorOfPoint maxContour = getMaxContour(src);
            if (maxContour != null){
                // Shape features
                Rectangle bounds = CvInvoke.BoundingRectangle(maxContour);
                CircleF circle = CvInvoke.MinEnclosingCircle(maxContour);
                RotatedRect ellipse = CvInvoke.FitEllipse(maxContour);
                float area = Convert.ToSingle(CvInvoke.ContourArea(maxContour, false));
                float perimeter = Convert.ToSingle(CvInvoke.ArcLength(maxContour, true));
                float shape = Convert.ToSingle((4 * Math.PI * area) / Math.Pow(perimeter, 2));
                int npoints = maxContour.Size;                    
                float width = bounds.Width;
                float height = bounds.Height;
                float centroideY = circle.Center.Y;
                float centroideX = circle.Center.X;
                float radius = circle.Radius;
                float aspectRatio = width / height;
                float extent = Convert.ToSingle(area / (width * height));
                float eccentricity = Convert.ToSingle(ellipse.Size.Height / ellipse.Size.Width);
                //float density = Convert.ToSingle(CvInvoke.CountNonZero(mask));
                //float equivDiameter = Convert.ToSingle(Math.Sqrt(4 * area / Math.PI));                    
                PointF[] hull = CvInvoke.ConvexHull(maxContour.ToArray().Select(pt => new PointF(pt.X, pt.Y)).ToArray());
                float solidity = 0;
                using (VectorOfPoint hullContourn = new VectorOfPoint(hull.Select(pt => new Point((int)pt.X, (int)pt.Y)).ToArray()))
                    solidity = Convert.ToSingle(area / CvInvoke.ContourArea(hullContourn, false));

                vectorOfFeatures.AddRange(new float[] {
                    area, shape, perimeter, npoints, width, height, centroideY, centroideX, radius, aspectRatio, extent
                });
            }         
           
            return vectorOfFeatures.ToArray();
        }


        public static float[] colorFeatures(Image<Bgr, Byte> src){
            List<float> vectorOfFeatures = new List<float>();
            VectorOfPoint maxContour = getMaxContour(src);
            if (maxContour != null){
                CircleF circle = CvInvoke.MinEnclosingCircle(maxContour);
                using (Image<Hsv, Byte> hsv = src.Convert<Hsv, Byte>()) {
                        MCvScalar hsvColor = new MCvScalar(){
                        V0 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 0],
                        V1 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 1],
                        V2 = hsv.Data[(int)circle.Center.Y, (int)circle.Center.X, 2]
                    };
                }                
                MCvScalar bgrColor = new MCvScalar(){
                    V0 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 0],
                    V1 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 1],
                    V2 = src.Data[(int)circle.Center.Y, (int)circle.Center.X, 2]
                };
                vectorOfFeatures.AddRange(new float[] {
                    
                });

            }
            return vectorOfFeatures.ToArray();
        }

        public static double[] textureFeatures(Image<Bgr, Byte> src){
            using (Image<Gray, Byte> gray = src.Convert<Gray, Byte>()){
                var glcm = new GrayLevelCooccurrenceMatrix(distance: 1, degree: CooccurrenceDegree.Degree0, normalize: true);                
                double[,] matrix = glcm.Compute(gray.ToBitmap());
                HaralickDescriptor haralick = new HaralickDescriptor(matrix);
                //double[] haralickFeatures = haralick.GetVector();
                double homogeneity = haralick.AngularSecondMomentum;
                double contrast = haralick.Contrast;
                return new double[] { homogeneity, contrast };
            }
        }


        public static Matrix<float> vector2matrix(float[] features)
        {
            Matrix<float> row = new Matrix<float>(new Size(features.Length, 1));
            for (int x = 0; x < features.Length; x++)
            {
                row[0, x] = features[x];
            }
            return row;
        }

    }
}
