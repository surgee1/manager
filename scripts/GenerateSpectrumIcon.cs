using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class GenerateSpectrumIcon
{
    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath(); float d = r * 2;
        path.AddArc(x,y,d,d,180,90); path.AddArc(x+w-d,y,d,d,270,90); path.AddArc(x+w-d,y+h-d,d,d,0,90); path.AddArc(x,y+h-d,d,d,90,90); path.CloseFigure(); return path;
    }
    static void Main(string[] args)
    {
        var images = new List<byte[]>(); int[] sizes = {16,24,32,48,64,128,256};
        foreach(int size in sizes)
        using(var bitmap = new Bitmap(size,size,PixelFormat.Format32bppArgb))
        using(var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; graphics.ScaleTransform(size/256f,size/256f);
            using(var shape=Round(5,5,246,246,58))
            using(var fill=new SolidBrush(Color.FromArgb(16,20,31)))
            using(var outline=new Pen(Color.FromArgb(52,65,88),3)) {graphics.FillPath(fill,shape);graphics.DrawPath(outline,shape);}
            using(var s = new GraphicsPath())
            {
                s.AddBezier(181,74,147,43,88,47,73,77); s.AddBezier(73,77,50,115,108,120,139,131);
                s.AddBezier(139,131,189,147,179,187,139,194); s.AddBezier(139,194,105,201,81,190,65,175);
                using(var gradient=new LinearGradientBrush(new Rectangle(0,0,256,256),Color.FromArgb(88,214,255),Color.FromArgb(159,111,255),LinearGradientMode.ForwardDiagonal))
                using(var pen=new Pen(gradient,31) { StartCap=LineCap.Round, EndCap=LineCap.Round, LineJoin=LineJoin.Round }) graphics.DrawPath(pen,s);
            }
            using(var memory=new MemoryStream()) {bitmap.Save(memory,ImageFormat.Png); images.Add(memory.ToArray());}
            if(size==256) bitmap.Save(args[1],ImageFormat.Png);
        }
        using(var stream=File.Create(args[0])) using(var writer=new BinaryWriter(stream))
        {
            writer.Write((short)0);writer.Write((short)1);writer.Write((short)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)1);writer.Write((short)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(var bytes in images) writer.Write(bytes);
        }
    }
}
