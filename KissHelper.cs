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

        public KinectSensor Sensor;
        public WriteableBitmap ColorBitmap { get; private set; }
        byte[] colorPixels;
        Skeleton[] skeletons;
        int dataAge;

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
    }
}
