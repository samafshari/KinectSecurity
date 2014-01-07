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
        KissHelper kiss;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            KissHelper.Initialize();
            kiss = KissHelper.DefaultHelper;
            kiss.OnCapture = OnCapture;
            kiss.ShootFrame();
        }

        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            kiss.ShootFrame();
        }

        void OnCapture(object o)
        {
            this.statusBarText.Text = (string)o;
            Image.Source = kiss.ColorBitmap;
        }
    }
}