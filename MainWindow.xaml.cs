//------------------------------------------------------------------------------
//
// Kinect Security System / KISS - Based on ColorBasics code from Kinect Toolkit
//
// January 2014, Saeed Afshari saeed@saeedoo.com
//
//------------------------------------------------------------------------------
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
    public partial class MainWindow : Window
    {
        private KinectSensor sensor;
        private WriteableBitmap colorBitmap;
        
        private byte[] colorPixels;
        DispatcherTimer timer = new DispatcherTimer();
        Skeleton[] skeletons = null;
        int dataAge = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            timer.Tick += timer_Tick;
            timer.Interval = new TimeSpan(0,0,0,0,Properties.Settings.Default.TickMsec);
            timer.Start();

            if (null != this.sensor)
            {
                sensor.SkeletonStream.Enable();
                sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(sensor.ColorStream.FrameWidth, sensor.ColorStream.FrameHeight, 8.0, 8.0, PixelFormats.Bgr32, null);
                this.Image.Source = this.colorBitmap;

                this.sensor.ColorFrameReady += this.SensorColorFrameReady;
                this.sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;
                
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            this.Title = trackedCount.ToString();
            trackedCount = 0;
            
            if (dataAge < Properties.Settings.Default.MaxEmptyFrames)
            {
                if (skeletons == null) return;
                foreach (var item in skeletons)
                    if (item.TrackingState == SkeletonTrackingState.Tracked) trackedCount++;
            }
            dataAge++;
            if (trackedCount > 0 && !sensor.ColorStream.IsEnabled)
                sensor.ColorStream.Enable();
            else if (trackedCount == 0 && sensor.ColorStream.IsEnabled)
                sensor.ColorStream.Disable();
            
            if (sensor.ColorStream.IsEnabled)
                ButtonScreenshotClick(this, new RoutedEventArgs());
        }

        int trackedCount = 0;
        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var frame = e.OpenSkeletonFrame();
            if (frame != null)
            {
                dataAge = 0;
                skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor) this.sensor.Stop();
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
            string myPhotos = KissHelper.GetPath();
            string path = Path.Combine(myPhotos, "kiss-" + time + ".jpg");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create)) encoder.Save(fs);
                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }
    }
}