using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Run from an isolated directory containing the built manager and its dependencies.
class FrameSmoke
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    static IntPtr Coordinates(Point point) { return (IntPtr)unchecked((int)((point.X & 0xffff) | ((point.Y & 0xffff) << 16))); }
    static void Check(bool value,string text) { if(!value)throw new Exception(text);Console.WriteLine("PASS "+text); }
    static void Pump() { var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<100){Application.DoEvents();System.Threading.Thread.Sleep(5);} }
    static int Hit(Form form,Point point) { point.Offset(form.Left,form.Top);return SendMessage(form.Handle,0x84,IntPtr.Zero,Coordinates(point)).ToInt32(); }
    static Point Button(Form form,int index) { int scale=form.DeviceDpi;int inset=form.PointToScreen(Point.Empty).X-form.Left-Math.Max(1,scale/96);return new Point(form.Width-inset-(int)Math.Round((29+42*index)*scale/96f),inset+(int)Math.Round(21*scale/96f)); }
    static void Click(Form form,int index,int expected)
    {
        var point=Button(form,index);Check(Hit(form,point)==expected,"caption button hit target: "+expected);
        var screen=point;screen.Offset(form.Left,form.Top);
        SendMessage(form.Handle,0xA1,(IntPtr)expected,Coordinates(screen));
        SendMessage(form.Handle,0x202,(IntPtr)1,Coordinates(form.PointToClient(screen)));Pump();
    }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            var assembly=Assembly.LoadFrom("Roblox Account Manager.exe");
            var frame=assembly.GetType("RBX_Alt_Manager.Classes.RoundedWindow");
            using(var form=new Form{Text="Spectrum frame check",ClientSize=new Size(520,300),MinimumSize=new Size(300,200),BackColor=Color.FromArgb(16,18,23)})
            {
                frame.GetMethod("Attach",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{form});form.Show();Pump();
                var origin=form.PointToScreen(Point.Empty);int caption=(int)Math.Round(42*form.DeviceDpi/96f);
                Check(origin.Y-form.Top==caption&&origin.X-form.Left<=2,"custom client origin replaces the legacy caption and thick border");
                Check(form.ClientSize==new Size(520,300),"frame replacement preserves initial content size");
                Check(Hit(form,new Point(200,20))==2,"title strip keeps native dragging");
                Check(Hit(form,new Point(1,form.Height/2))==10&&Hit(form,new Point(form.Width-3,form.Height-3))==17,"side and corner keep native resizing");
                Check(form.Region!=null&&!form.Region.IsVisible(0,0),"normal window has rounded corners");
                Click(form,1,9);Check(form.WindowState==FormWindowState.Maximized&&form.Region==null,"maximize button fills working area without clipped corners");
                var work=Screen.FromControl(form).WorkingArea;var client=form.RectangleToScreen(form.ClientRectangle);Check(client==new Rectangle(work.X+1,work.Y+caption,work.Width-2,work.Height-caption-1),"maximized content and caption remain completely inside monitor working area");
                Click(form,1,9);Check(form.WindowState==FormWindowState.Normal&&form.Region!=null,"restore button restores rounded shape");
                Click(form,2,8);Check(form.WindowState==FormWindowState.Minimized,"minimize button executes native command");
                form.WindowState=FormWindowState.Normal;Pump();
                form.Size=new Size(630,390);Pump();Check(form.Region!=null&&form.Region.IsVisible(form.Width/2,form.Height/2),"resized frame retains complete content");
                Click(form,0,20);Check(form.IsDisposed,"close button closes the window");
            }
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}
    }
}
