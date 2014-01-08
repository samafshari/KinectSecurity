using System.Windows.Threading;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Diagnostics;
using AviFile;
using System.Drawing;
namespace Kiss
{
    public class KissHelper
    {
        public static KissHelper DefaultHelper = null;
        public static void Initialize() { DefaultHelper = new KissHelper(); }
        public KinectSensor Sensor { get; private set; }
        public WriteableBitmap ColorBitmap { get; private set; }

        byte[] colorPixels;
        Skeleton[] skeletons;
        int dataAge;
        public DispatcherTimer Timer { get; private set; }
        public int TrackedSkeletonsCount { get; private set; }

        public Action<object> OnCapture;
        public Action<object> OnStart;
        public Action<object> OnStop;

        AviManager aviManager;
        VideoStream aviStream;
        public static string GetPath()
        {
            string currentPath = Properties.Settings.Default.FolderPath.Trim();
            if (!Directory.Exists(currentPath))
            {
                currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                Properties.Settings.Default.FolderPath = currentPath;
                Properties.Settings.Default.Save();
            }

            currentPath = Path.Combine(currentPath, Properties.Settings.Default.FolderName);
            if (!Directory.Exists(currentPath)) Directory.CreateDirectory(currentPath);

            return currentPath;
        }

        public KissHelper(KinectSensor sensor = null)
        {
            if (sensor == null)
            {
                foreach (var potentialSensor in KinectSensor.KinectSensors)
                    if (potentialSensor.Status == KinectStatus.Connected)
                    {
                        sensor = potentialSensor;
                        break;
                    }
            }

            Sensor = sensor;
            OnCapture = null;
            OnStart = null;
            OnStop = null;
            if (Sensor == null) throw new Exception("Sensor not found.");

            Timer = new DispatcherTimer();
            Timer.Tick += timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 0, 0, (int)Properties.Settings.Default.TickMsec);
            Timer.Start();

            Sensor.SkeletonStream.Enable();
            Sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

            Sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            colorPixels = new byte[Sensor.ColorStream.FramePixelDataLength];
            ColorBitmap = new WriteableBitmap(
                Sensor.ColorStream.FrameWidth, Sensor.ColorStream.FrameHeight,
                8.0, 8.0, PixelFormats.Bgr32, null);

            Sensor.ColorFrameReady += Sensor_ColorFrameReady;
            Sensor.SkeletonFrameReady += Sensor_SkeletonFrameReady;

            Sensor.Start();
            Sensor.ColorStream.Disable();
        }

        void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (var frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    dataAge = 0;
                    skeletons = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(skeletons);
                }
            }
        }

        void Sensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    ColorBitmap.WritePixels(
                        new Int32Rect(0, 0, ColorBitmap.PixelWidth, ColorBitmap.PixelHeight),
                        colorPixels, ColorBitmap.PixelWidth * sizeof(int), 0);
                }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            TrackedSkeletonsCount = 0;

            if (dataAge++ < Properties.Settings.Default.MaxEmptyFrames)
            {
                if (skeletons == null) return;
                
                foreach (var item in skeletons)
                    if (item.TrackingState == SkeletonTrackingState.Tracked) TrackedSkeletonsCount++;
            }

            if (TrackedSkeletonsCount > 0 && !Sensor.ColorStream.IsEnabled) Start();
            else if (TrackedSkeletonsCount == 0 && Sensor.ColorStream.IsEnabled) Stop();

            if (Sensor.ColorStream.IsEnabled) ShootFrame();
        }

        public void Start()
        {
            Debug.WriteLine("Starting...");

            if (aviStream != null || aviManager != null)
            {
                Debug.WriteLine("AviManager is not null!");
            }

            Sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string path = Path.Combine(KissHelper.GetPath(), "kiss-" + time + ".avi");

            
            aviManager = new AviManager(path, false);
            
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ColorBitmap));
            MemoryStream s = new MemoryStream();
            encoder.Save(s);
            Bitmap b = (Bitmap)Image.FromStream(s);
            Avi.AVICOMPRESSOPTIONS opts = new Avi.AVICOMPRESSOPTIONS();
            opts.fccType = 0; // (UInt32)Avi.mmioStringToFOURCC("mrle", 0);
            opts.fccHandler = 1684633187;// (UInt32)Avi.mmioStringToFOURCC("MRLE", 0);

            opts.dwKeyFrameEvery = 0;
            opts.dwQuality = 10000;  // 0 .. 10000
            opts.dwFlags = 10;  // AVICOMRPESSF_KEYFRAMES = 4
            opts.dwBytesPerSecond = 204800;
            opts.lpFormat = new IntPtr(0);
            opts.cbFormat = 0;
            opts.lpParms = new IntPtr(0);
            opts.cbParms = 4;
            opts.dwInterleaveEvery = 0;

            aviStream = aviManager.AddVideoStream(opts, 1000.0 / Properties.Settings.Default.TickMsec, b);

            if (OnStart != null) OnStart(null);

            Debug.WriteLine("Started.");
        }

        public void Stop()
        {
            Debug.WriteLine("Stopping...");
            if (aviManager != null)
            {
                aviManager.Close();
            }
            else
            {
                Debug.WriteLine("aviManager is null. Cannot close.");
            }
            aviManager = null;
            aviStream = null;
            Sensor.ColorStream.Disable();
            if (OnStop != null) OnStop(null);
            Debug.WriteLine("Stopped.");
        }

        public void ShootFrame(bool photo = false)
        {
            if (Sensor == null) return;

            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ColorBitmap));

            if (photo)
                SaveImage(encoder);
            else
                AddVideoFrame(encoder);

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            if (OnCapture != null) OnCapture(time + " captured.");
        }

        bool AddVideoFrame(BitmapEncoder encoder)
        {
            if (aviStream == null)
            {
                Debug.WriteLine("aviStream is null.");
                return false;
            }

            using (MemoryStream s = new MemoryStream())
            {
                encoder.Save(s);
                Bitmap b = (Bitmap)Image.FromStream(s);
                aviStream.AddFrame(b);
                b.Dispose();
            }

            return true;
        }

        bool SaveImage(BitmapEncoder encoder)
        {
            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string path = Path.Combine(KissHelper.GetPath(), "kiss-" + time + ".jpg");
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create)) encoder.Save(fs);
            }
            catch (IOException)
            {
                return false;
            }
            return true;
        }

        ~KissHelper()
        {
            if (Sensor != null) Sensor.Stop();
        }
    }
}
