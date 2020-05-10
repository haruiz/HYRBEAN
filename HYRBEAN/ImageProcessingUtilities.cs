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
    public class ImageProcessingUtilities {


        public static Task<Image<Bgr, Byte>> CorrectLightness(Image<Bgr, Byte> rgb)
        {
            return Task.Run(delegate () {
                Image<Lab, Byte> lab = rgb.Convert<Lab, Byte>();
                Image<Gray, Byte>[] lab_planes = lab.Split();
                Image<Gray, Byte> lightness = new Image<Gray, byte>(rgb.Size);
                CvInvoke.CLAHE(lab_planes[0], 40, new Size(4, 4), lightness);
                VectorOfMat vm = new VectorOfMat(lightness.Mat, lab_planes[1].Mat, lab_planes[2].Mat);
                CvInvoke.Merge(vm, lab);
                Image<Bgr, Byte> dst = lab.Convert<Bgr, Byte>();
                vm.Dispose();
                lab.Dispose();
                lab_planes = null;
                lightness.Dispose();
                return dst;
            });

        }

        /*
          input:
          strength as floating point >= 0.  0 = no change, high numbers equal stronger correction.
          zoom as floating point >= 1.  (1 = no change in zoom)
          */
        public static Task<Image<Bgr, Byte>> correctLentDistorsion(Image<Bgr, Byte> src, float strength, float zoom = 1)
        {
            return Task.Run(delegate () {
                double halfWidth = src.Width / 2;
                double halfHeight = src.Height / 2;
                double theta = 0;
                strength = strength == 0 ? Single.Epsilon : strength;
                double correctionRadius = Math.Sqrt(Math.Pow(src.Width, 2) + Math.Pow(src.Height, 2)) / strength;

                Image<Bgr, double> copy = src.Convert<Bgr, double>();
                Image<Bgr, double> blank = copy.CopyBlank();

                double[,,] srcData = copy.Data;
                double[,,] dstData = blank.Data;

                int rows = copy.Rows;
                int cols = copy.Cols;
                for (int y = 0; y < rows; ++y)
                {
                    for (int x = 0; x < cols; ++x)
                    {
                        double newX = Convert.ToDouble(x) - halfWidth;
                        double newY = Convert.ToDouble(y) - halfHeight;
                        double distance = Math.Sqrt(Math.Pow(newX, 2) + Math.Pow(newY, 2));
                        double r = distance / correctionRadius;
                        theta = r == 0 ? 1 : Math.Atan(r) / r;

                        //Console.WriteLine(r);
                        int sourceX = (int)(halfWidth + theta * newX * zoom);
                        int sourceY = (int)(halfHeight + theta * newY * zoom);

                        //Console.WriteLine(String.Format("{0},{1}",sourceX, sourceY));
                        dstData[y, x, 0] = srcData[sourceY, sourceX, 0];
                        dstData[y, x, 1] = srcData[sourceY, sourceX, 1];// / (data[y, x, 0] + data[y, x, 1] + data[y, x, 2]);
                        dstData[y, x, 2] = srcData[sourceY, sourceX, 2];
                    }
                }
                copy.Dispose();
                return blank.Convert<Bgr, byte>();
            });
        }

        public static Task<Image<Bgr, Byte>> rgb2ChromaticCoordinates(Image<Bgr, Byte> img){
            return Task.Run(delegate () {
                using (Image<Bgr, double> copy = img.Convert<Bgr, double>())
                {
                    double[,,] data = copy.Data;
                    int rows = copy.Rows;
                    int cols = copy.Cols;
                    for (int y = 0; y < rows; ++y)
                    {
                        for (int x = 0; x < cols; ++x)
                        {
                            double sum = data[y, x, 0] + data[y, x, 1] + data[y, x, 2];
                            data[y, x, 0] = data[y, x, 0] / sum * 255;
                            data[y, x, 1] = data[y, x, 1] / sum * 255;// / (data[y, x, 0] + data[y, x, 1] + data[y, x, 2]);
                            data[y, x, 2] = data[y, x, 2] / sum * 255; // / (data[y, x, 0] + data[y, x, 1] + data[y, x, 2]); 
                        }
                    }
                    Image<Bgr, Byte> dst = copy.Convert<Bgr, Byte>();
                    return dst;
                }
            });
        }


    }
}
