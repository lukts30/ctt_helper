using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ctt_helper
{
    class Program
    {
        static void TestDimension() {
            var buffer = ReadUnromBuffer("C:\\Users\\lukas\\Desktop\\100_25_pixel.bin");
            var cDstUnorm = Create2DArrayFrom1D(buffer);
            Console.WriteLine("{0}",cDstUnorm.Length);
            Console.WriteLine("ww {0}",TEXELFETCH2D(cDstUnorm,14,2));
        }

        static void WriteTexToFile(float[,] tex,string file) {
            var float1D = Create1DArrayFrom2D(tex);
            var arraySpanFloat = new Span<float>(float1D);
            Span<byte> bytesView = MemoryMarshal.Cast<float, byte>(arraySpanFloat);
            File.WriteAllBytes(file, bytesView.ToArray());
        }

         static void ReadFileAsUnormTex(string file,out ushort[,] cDstUnorm) {
            var buffer = ReadUnromBuffer(file);
            cDstUnorm = Create2DArrayFrom1D(buffer);
         }

        static void ReadFileAsUnormToFloat(string file,out float[,] cDstFloat) {
            ReadFileAsUnormTex(file,out ushort[,] cDstUnorm);
            csCopyUnormToFloat(cDstUnorm,out cDstFloat);  
         } 


        static void Main(string[] args)
        {
            ReadFileAsUnormToFloat("D:\\Downsampled_66.bin",out float[,] cDstFloat);  
            WriteTexToFile(cDstFloat,"float_66");

            RunCsSynthesize(cDstFloat);
        }

        static void RunCsSynthesize(float[,] cSrc2) {
            var buffer = ReadUnromBuffer("D:\\unrom_132.bin");
            var cSrc1 = Create2DArrayFrom1D(buffer);
            csSynthesize(cSrc1,cSrc2, out float[,] cDstFloat);
            Console.WriteLine("132: {0}",TEXELFETCH2D(cDstFloat,130,131));

            WriteTexToFile(cDstFloat,"float_132");
        }

        static Span<ushort> ReadUnromBuffer(string fileName) {
            byte[] buff = File.ReadAllBytes(fileName);
            var arraySpan = new Span<byte>(buff);
            Span<ushort> ushortView = MemoryMarshal.Cast<byte, ushort>(arraySpan);
            return ushortView;
        }

        static ushort[,] Create2DArrayFrom1D(Span<ushort> input) {
            int length = (int)Math.Sqrt((double) input.Length);
            
            ushort[,] cDstUnorm = new ushort[length,length];
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < length; j++)
                {
                    var idx = i*length+j;
                    cDstUnorm[i,j] = input[idx];
                }
            }
            return cDstUnorm;
        }

        static float[] Create1DArrayFrom2D(float[,] input) {
            int length_rk = input.GetLength(0);
            int length = input.Length;
            
            
            float[] cDst1D = new float[length];
            for (int i = 0; i < length_rk; i++)
            {
                for (int j = 0; j < length_rk; j++)
                {
                    var idx = i*length_rk+j;
                    
                    cDst1D[idx] = input[i,j] ;
                }
            }
            return cDst1D;
        }

        static void csCopyUnormToFloat(ushort[,] cSrc1,out float[,] cDstFloat)
        {
	        // cDstFloat[dtID.xy] = TEXELFETCH2D(cSrc1, dtID.xy, 0).r;
            int length = cSrc1.GetLength(0);
            Debug.Assert(cSrc1.GetLength(0) == cSrc1.GetLength(1));
            cDstFloat = new float[length,length];

            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    cDstFloat[y,x] = TEXELFETCH2D(cSrc1,x,y);
                }
            }
        }

        static float TEXELFETCH2D(ushort[,] cSrc1,int x,int y) {
            return UnromToFloat(cSrc1[y,x]);
        }

        static float TEXELFETCH2D(float[,] cSrc1,int x,int y) {
            return cSrc1[y,x];
        }

        static float UnromToFloat(ushort unorm) {
            return (float) unorm / (float) ushort.MaxValue;
        }

        static void csSynthesize(ushort[,] cSrc1,float[,] cSrc2,out float[,] cDstFloat)
        {
            int length = cSrc1.GetLength(0);
            cDstFloat = new float[length,length];
            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    //float lo = SampleBicubic(dtID.xy * 0.5 - 0.25, cSrc2, cSrc2Size);
                    var dtX = x * 0.5f - 0.25f;
                    var dtY = y * 0.5f - 0.25f;

                    float lo = SampleBicubic(dtX,dtY, cSrc2, cSrc2.GetLength(0), cSrc2.GetLength(0));
                    float hi = TEXELFETCH2D(cSrc1, x,y);

                    //hi = Dequantize(hi, cCurrentMip); // (val * 2 - 1);
                    hi = (hi * 2f - 1f);
                    //cDstFloat[dtID.xy] = lo + hi;
                    cDstFloat[y,x] = lo + hi;
                }
            }
        }

        static float SampleBicubic(float coordX,float coordY, float[,] tex, int sizeX,int sizeY) {

            int xi = (int)Math.Floor(coordX);
            int yi = (int)Math.Floor(coordY);

            // Fetch 4x4 samples
            float result = 0.0f;
            float wsum   = 0.0f;
            for (int j = yi - 1; j <= yi + 2; ++j)
            {
                for (int i = xi - 1; i <= xi + 2; ++i)
                {
                    var m = GetMirrorOnceCoords(i, j,sizeX,sizeY);
                    float sample = TEXELFETCH2D(tex, m.coordX,m.coordY);
                    float w      = GetBicubicWeight2D_Hermite(i, j, coordX,coordY);

                    result += w * sample;
                    wsum   += w;
                }
            }
            if (wsum > 0.0)
            {
                result /= wsum;
            }

	        return result;
        }

        /// Loads a texture's texel with mirror-once sampling
        /** 
            Note: Only works correctly in the (-size, 2*size-2) range!
                So use this function only for sampling the texture's inner area and a bit beyond the borders.
        */
        static (int coordX,int coordY) GetMirrorOnceCoords(int coordX,int coordY, int sizeX,int sizeY)
        {
            // Mirror around (0, 0)
            coordX = Math.Abs(coordX);
            coordY = Math.Abs(coordY);

            // Mirror around (w, h)
            if (coordX >= sizeX)
            {
                coordX = 2 * (sizeX - 1) - coordX;
            }

            if (coordY >= sizeY)
            {
                coordY = 2 * (sizeY - 1) - coordY;
            }

            return (coordX,coordY);
        }

        /// Helper function for bicubic texture sampling functions (i.e. SampleTextureBicubicR())
        static float GetBicubicWeight2D_Hermite(float samplePosX,float samplePosY,float centerPosX,float centerPosY)
        {
            var tx = Math.Abs(samplePosX - centerPosX);
            var ty = Math.Abs(samplePosY - centerPosY);
            float wx = GetBicubicWeight1D_Hermite(tx);
            float wy = GetBicubicWeight1D_Hermite(ty);

            return wx * wy;
        }

        /// Helper function for bicubic texture sampling functions (i.e. SampleTextureBicubicR())
        static float GetBicubicWeight1D_Hermite(float x)
        {
            float x2 = x * x;
            float x3 = x * x2;

            // Hermite spline
            // From https://en.wikipedia.org/wiki/Bicubic_interpolation with a = -0.5 for cubic hermite spline
            const float a = -0.5f;

            if (x <= 1.0)
            {
                return (a + 2) * x3 - (a + 3) * x2 + 1;
            }
            else
            if (x < 2.0)
            {
                return (a * x3) - (5 * a * x2) + (8 * a * x) - (4 * a);
            }

            // 	// B-spline
            // 	if (x <= 1.0)
            // 	{
            // 		return 2.0 / 3.0 + 0.5 * x3 - x2;
            // 	}
            // 	else
            // 	if (x < 2.0)
            // 	{
            // 		float t = (2.0 - x);
            // 
            // 		return 1.0 / 6.0 * t * t * t;
            // 	}

            return 0.0f;
        }
    }
    
}
