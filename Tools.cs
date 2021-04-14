﻿using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XML2ESCPOS
{
    public static class Tools
    {
        /* <RESET/>
        * <END/>
        * <LEFT/>
        * <CENTER/>
        * <RIGHT/>
        * <DHEIGHT></DHEIGHT>
        * <DWIDTH></DWIDTH>
        * <BOLD></BOLD>
        * <U></U> UNDERLINE
        * <U2></U2> UNDERLINE 2-DOTS
        * <BR/> NUEVA LINE \n
        * <DEFTABS N1='10',N2='20'.. />
        * <TAB>
        * <IMAGE PATH='imagen con path relativo al directorio actual'/>
        * <![CDATA[Nombre variable]/>
        * <LOOP VARNAME='Nombre variable IEnumerable'></>
        * <![CDATA[Nombre variable IEnumerable.Propiedad]/>
        */

        public static int ToNumeral(this BitArray binary)
        {
            if (binary == null)
                throw new ArgumentNullException("binary");
            if (binary.Length > 32)
                throw new ArgumentException("must be at most 32 bits long");

            var result = new int[1];
            binary.CopyTo(result, 0);
            return result[0];
        }


        public static IEnumerable<byte> GetImageBytes(string imagePath, int factor)
        {
            BitmapData data = GetBitmapData(imagePath, factor);
            BitArray dots = data.Dots;

            byte[] width = BitConverter.GetBytes(data.Width);
            byte[] bytes;
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(stream))
                {
                    binaryWriter.Write((char)0x1B);
                    binaryWriter.Write('@');
                    binaryWriter.Write((char)0x1B);
                    binaryWriter.Write('3');
                    binaryWriter.Write((byte)24);

                    int offset = 0;
                    while (offset < data.Height)
                    {
                        binaryWriter.Write((char)0x1B);
                        binaryWriter.Write('*'); // bit-image mode
                        binaryWriter.Write((byte)33); // 24-dot double-density
                        binaryWriter.Write(width[0]); // width low byte
                        binaryWriter.Write(width[1]); // width high byte

                        for (int x = 0; x < data.Width; ++x)
                        {
                            for (int k = 0; k < 3; ++k)
                            {
                                byte slice = 0;
                                for (int b = 0; b < 8; ++b)
                                {
                                    int y = (offset / 8 + k) * 8 + b;
                                    // Calculate the location of the pixel we want in the bit array.
                                    // It'll be at (y * width) + x.
                                    int i = (y * data.Width) + x;

                                    // If the image is shorter than 24 dots, pad with zero.
                                    bool v = false;
                                    if (i < dots.Length)
                                    {
                                        v = dots[i];
                                    }

                                    slice |= (byte)((v ? 1 : 0) << (7 - b));
                                }

                                binaryWriter.Write(slice);
                            }
                        }

                        offset += 24;
                        binaryWriter.Write((char)0x0A);
                    }

                    // Restore the line spacing to the default of 30 dots.
                    binaryWriter.Write((char)0x1B);
                    binaryWriter.Write('3');
                    binaryWriter.Write((byte)30);
                }

                bytes = stream.ToArray();
            }

            return bytes;
        }

        private static BitmapData GetBitmapData(string imagePath, int factor)
        {
            byte[] bytes = File.ReadAllBytes(imagePath);

            // Wrap the bytes in a stream
            using (SKBitmap imageBitmap = SKBitmap.Decode(bytes))
            {
                int index = 0;
                const int threshold = 127;
                double multiplier = Convert.ToDouble(factor); // This depends on your printer model. for Beiyang you should use 1000
                double scale = multiplier / imageBitmap.Width;

                int height = (int)(imageBitmap.Height * scale);
                int width = (int)(imageBitmap.Width * scale);
                int dimensions = width * height;

                BitArray dots = new BitArray(dimensions);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int scaledX = (int)(x / scale);
                        int scaledY = (int)(y / scale);

                        SKColor color = imageBitmap.GetPixel(scaledX, scaledY);

                        // Luminance means Lightning, We can make output brighter or darker with it.
                        int luminance = (int)(color.Red * 0.3 + color.Green * 0.16 + color.Blue * 0.114);
                        dots[index] = luminance < threshold;
                        index++;
                    }
                }

                return new BitmapData
                {
                    Dots = dots,
                    Height = (int)(imageBitmap.Height * scale),
                    Width = (int)(imageBitmap.Width * scale)
                };
            }
        }

        private class BitmapData
        {
            public BitArray Dots { get; set; }

            public int Height { get; set; }

            public int Width { get; set; }
        }
    }
}
