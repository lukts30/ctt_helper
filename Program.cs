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

        static void Main(string[] args)
        {
            TestDimension();
            
            var buffer = ReadUnromBuffer("D:\\Downsampled_66.bin");
            var cDstUnorm = Create2DArrayFrom1D(buffer);
            Console.WriteLine("{0}",cDstUnorm[0,65]);
            
            csCopyUnormToFloat(cDstUnorm,out float[,] cDstFloat);
            Console.WriteLine("{0}",cDstFloat[64,65]);

            var float1D = Create1DArrayFrom2D(cDstFloat);
            var arraySpanFloat = new Span<float>(float1D);
            Span<byte> bytesView = MemoryMarshal.Cast<float, byte>(arraySpanFloat);
            File.WriteAllBytes("floats", bytesView.ToArray());
        }

        static Span<ushort> ReadUnromBuffer(string fileName) {
            byte[] buff = File.ReadAllBytes(fileName);
            var arraySpan = new Span<byte>(buff);
            Span<ushort> ushortView = MemoryMarshal.Cast<byte, ushort>(arraySpan);
            Console.WriteLine("{0}",ushortView[^1]);
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
                    //Console.WriteLine("{0}",idx);
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

        static float UnromToFloat(ushort unorm) {
            return (float) unorm / (float) ushort.MaxValue;
        }
    }
    
}
