using System.Windows.Threading;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

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
        DispatcherTimer timer;
        int trackedCount;

        public Action OnCapture;

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

            if (Sensor == null) throw new Exception("Sensor not found.");

            timer = new DispatcherTimer();
            timer.Tick += timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 0, 0, Properties.Settings.Default.TickMsec);
            timer.Start();

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
        }

        void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var frame = e.OpenSkeletonFrame();
            if (frame != null)
            {
                dataAge = 0;
                skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);
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
            trackedCount = 0;

            if (dataAge++ < Properties.Settings.Default.MaxEmptyFrames)
            {
                if (skeletons == null) return;
                foreach (var item in skeletons)
                    if (item.TrackingState == SkeletonTrackingState.Tracked) trackedCount++;
            }

            if (trackedCount > 0 && !Sensor.ColorStream.IsEnabled)
                Sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            else if (trackedCount == 0 && Sensor.ColorStream.IsEnabled)
                Sensor.ColorStream.Disable();

            if (Sensor.ColorStream.IsEnabled) ShootFrame();
        }

        public void ShootFrame()
        {
            if (Sensor != null) return;

            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ColorBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string myPhotos = KissHelper.GetPath();
            string path = Path.Combine(myPhotos, "kiss-" + time + ".jpg");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create)) encoder.Save(fs);
                //this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                //this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }

            if (OnCapture != null) OnCapture();
        }

        public ~KissHelper()
        {
            if (Sensor != null) Sensor.Stop();
        }
    }
}
