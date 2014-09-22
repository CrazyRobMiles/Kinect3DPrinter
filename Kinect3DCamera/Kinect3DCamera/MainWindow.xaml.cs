using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using System.IO;

// Kinect 3D Printer
// Takes 3D pictures and makes them into STL files for printing on a 3D printer
// Rob Miles September 2014

namespace Kinect3DCamera
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor = null;
        DepthFrameReader depth = null;

        ushort[] depthData = null;
        WriteableBitmap depthImageBitmap = null;
        byte[] depthColorImage = null;
        ushort[] gridHeights = null;

        bool takeSnapShot = false;
        DateTime lastSnapshotTime = DateTime.Now;
        string filepath;

        private bool Setup()
        {
            filepath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            sensor = KinectSensor.GetDefault();
            sensor.IsAvailableChanged += sensor_IsAvailableChanged;
            depth = sensor.DepthFrameSource.OpenReader();
            depth.FrameArrived += depth_FrameArrived;
            sensor.Open();
            return true;
        }

        private void UpdateSensorStatus()
        {
            if(sensor.IsAvailable)
            {
                Title = "Kinect3DCamera - Kinect connected";
            }
            else
            {
                Title = "Kinect3DCamera - Kinect disconnected";
            }
        }

        void sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            UpdateSensorStatus();
        }

        void depth_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            using (DepthFrame frame = depth.AcquireLatestFrame())
            {

                if (frame == null)
                    return;

                int minDist = (int)NearSlider.Value;
                int maxDist = (int)FarSlider.Value;
                int modelHeight = (int)ModelHeightSlider.Value;
                int modelWidth = (int)ModelWidthSlider.Value;

                ushort max = depth.DepthFrameSource.DepthMaxReliableDistance;
                ushort min = depth.DepthFrameSource.DepthMinReliableDistance;

                double maxScale = max - maxDist;
                double minScale = minDist - min;
                double scale = maxDist - minDist;
                int intScale = maxDist - minDist;

                int width = frame.FrameDescription.Width;
                int height = frame.FrameDescription.Height;

                if (depthData == null)
                    depthData = new ushort[frame.FrameDescription.LengthInPixels];

                if(gridHeights == null)
                    gridHeights = new ushort[frame.FrameDescription.LengthInPixels];

                frame.CopyFrameDataToArray(depthData);

                if (depthColorImage == null)
                    depthColorImage = new byte[frame.FrameDescription.LengthInPixels * 4];

                int depthColorImagePos = 0;

                average = imageAverage(depthData);

                for (int i = 0; i < average.Length; i++)
                {
                    int depthValue = average[i];
                    // Check for the invalid values

                    if (depthValue > maxDist)
                    {
                        double fraction = ((double)depthValue - maxDist) / maxScale;
                        byte depthByte = (byte)(255 - (255.0 * fraction));
                        depthColorImage[depthColorImagePos++] = depthByte; // Blue
                        depthColorImage[depthColorImagePos++] = 0; // Green
                        depthColorImage[depthColorImagePos++] = 0; // Red
                        gridHeights[i] = (ushort)0;
                    }
                    else if (depthValue < minDist)
                    {
                        double fraction = ((double)depthValue - min) / minScale;
                        byte depthByte = (byte)(255 - (255.0 * fraction));
                        depthColorImage[depthColorImagePos++] = 0; // Blue
                        depthColorImage[depthColorImagePos++] = 0; // Green
                        depthColorImage[depthColorImagePos++] = depthByte; // Red
                        gridHeights[i] = (ushort)0;
                    }
                    else
                    {
                        int absoluteDepth = depthValue - minDist;
                        double fraction = ((double)absoluteDepth) / scale;
                        byte depthByte = (byte)(255 - (255.0 * fraction));
                        depthColorImage[depthColorImagePos++] = depthByte; // Blue
                        depthColorImage[depthColorImagePos++] = depthByte; // Green
                        depthColorImage[depthColorImagePos++] = depthByte; // Red
                        gridHeights[i] = (ushort)(intScale - absoluteDepth);
                    }
                    // transparency
                    depthColorImagePos++;
                }

                if (depthImageBitmap == null)
                {
                    this.depthImageBitmap = new WriteableBitmap(
                        frame.FrameDescription.Width,
                        frame.FrameDescription.Height,
                        96,  // DpiX
                        96,  // DpiY
                        PixelFormats.Bgr32,
                        null);

                    kinectDepthImage.Width = frame.FrameDescription.Width;
                    kinectDepthImage.Height = frame.FrameDescription.Height;
                    kinectDepthImage.Source = depthImageBitmap;
                }

                checkSelfie();

                if (takeSnapShot)
                {
                    takeSnapShot = false;

                    string filename = FileNameTextBox.Text.Trim();

                    if (filename.Length == 0)
                    {
                        MessageBox.Show("Please enter a filename", "File save failed");
                        return;
                    }

                    string fullFilename = filepath + "\\" + filename + ".stl";

                    try
                    {
                        StoreSTLMesh(gridHeights, (int)kinectDepthImage.Height, (int)kinectDepthImage.Width, modelWidth, modelHeight, fullFilename);
                        // Set the start of the screen flash
                        lastSnapshotTime = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("File name: " + fullFilename + " not written.\n" + ex.Message, "File save failed");
                    }
                }

                DateTime now = DateTime.Now;

                // Invert the screen for 200 milliseconds after taking a picture to show it has done something

                if((now-lastSnapshotTime).TotalMilliseconds<200)
                {
                    for (int i = 0; i < depthColorImage.Length; i++)
                    {
                        depthColorImage[i] ^= 255;
                    }
                }

                this.depthImageBitmap.WritePixels(
                    new Int32Rect(0, 0, frame.FrameDescription.Width, frame.FrameDescription.Height),
                    depthColorImage, // video data
                    frame.FrameDescription.Width * 4, // stride,
                    0   // offset into the array - start at 0
                    );

            }
        }

        #region Image Averaging

        ushort[] averageBuffer = null;

        ushort[] average;
        uint[] total;

        int maxAverages = 50;
        int averageSize = 0;
        int activePlane = 0;

        unsafe ushort[] imageAverage(ushort[] data)
        {

            if (averageSize == 0) return data;

            int bufferSize = data.Length;

            if (averageBuffer == null)
            {
                // Make the buffers for the averaging process
                averageBuffer = new ushort[bufferSize * maxAverages];
                average = new ushort[bufferSize];
                total = new uint[bufferSize];
            }

            fixed (ushort* averageBase = averageBuffer, resultBase = average, imageBase = data)
            {
                fixed (uint* totalBase = total)
                {
                    ushort* imagePosition = imageBase;
                    ushort* averagePosition = averageBase + (activePlane * bufferSize);
                    uint* totalPosition = totalBase;
                    ushort* imageEnd = imageBase + bufferSize;
                    ushort* resultPos = resultBase;

                    while (imagePosition != imageEnd)
                    {
                        // Get the average for this pixel
                        uint pixelTotal = *totalPosition;

                        // Get the latest value
                        ushort newPixel = *imagePosition;

                        // if the old value is invalid (zero) we ignore it

                        if (newPixel != 0)
                        {
                            // Subtract the old value we are replacing
                            pixelTotal -= *averagePosition;

                            // Add the new value to the total
                            pixelTotal += newPixel;

                            // Store it back in the total array
                            *totalPosition = pixelTotal;

                            // Store the new pixel value for removal later
                            *averagePosition = newPixel;
                        }

                        // work out the new average value
                        *resultPos = (ushort)(pixelTotal / averageSize);

                        // Move down the arrays
                        totalPosition++;
                        averagePosition++;
                        resultPos++;
                        imagePosition++;
                    }

                    // Move on to the next plane next time

                    activePlane++;
                    if (activePlane == averageSize)
                    {
                        activePlane = 0;
                    }
                }
            }
            return average;
        }

        private void clearTotal()
        {
            if (total != null)
            {
                for (int i = 0; i < total.Length; i++)
                    total[i] = 0;
            }
        }

        private void clearAverage()
        {
            if (averageBuffer != null)
            {
                for (int i = 0; i < averageBuffer.Length; i++)
                    averageBuffer[i] = 0;
            }
        }


        #endregion

        #region Mesh Generation

        public struct Vertex
        {
            public double X;
            public double Y;
            public double Z;
            public Vertex(double Xin, double Yin, double Zin)
            {
                X = Xin;
                Y = Yin;
                Z = Zin;
            }
            public void STLWrite(BinaryWriter b)
            {
                b.Write((float)X);
                b.Write((float)Y);
                b.Write((float)Z);
            }
        }

        public struct Triangle
        {
            public Vertex V1;
            public Vertex V2;
            public Vertex V3;
            public Triangle(Vertex V1in, Vertex V2in, Vertex V3in)
            {
                V1 = V1in;
                V2 = V2in;
                V3 = V3in;
            }

            public void STLWrite(BinaryWriter b)
            {
                V1.STLWrite(b);
                V2.STLWrite(b);
                V3.STLWrite(b);
            }
        }

        /// <summary>
        /// Takes a 2D array of double values and creates a mesh of triangles for a 3D plot of the data
        /// </summary>
        /// <param name="grid">source grid</param>
        /// <param name="modelWidth">width of the model in mm to be printed. The depth of the model is calculated automatically from the aspect ratio of the grid.</param>
        /// <param name="baseHeight">height of the base in mm</param>
        /// <param name="modelHeight">height of highest part of the model, in mm. The plotter finds the highest and lowest points in the grid and scales appropriately.</param>
        /// <returns>a list of triangles that make up the mesh</returns>
        public List<Triangle> SolidPlot(double[,] grid, double modelWidth, double baseHeight, double modelHeight)
        {
            int width = grid.GetLength(0);
            int depth = grid.GetLength(1);

            List<Triangle> result = new List<Triangle>();

            // Find the lowest and highest values 
            double hi = grid[0, 0];
            double lo = hi;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < depth; y++)
                {
                    if (grid[x, y] > hi) hi = grid[x, y];
                    if (grid[x, y] < lo) lo = grid[x, y];
                }
            }

            // Heights and steps

            double modelDepth = (double)modelWidth * ((double)depth / (double)width);
            double gridHeight = hi - lo;
            double heightScale = modelHeight / gridHeight;
            double xstep = modelWidth / width;
            double ystep = modelDepth / depth;

            // Add the base
            double bx = 0;
            double by = 0;

            for (int x = 0; x < width; x++)
            {
                by = 0;
                for (int y = 0; y < depth; y++)
                {
                    // Make the first triangle
                    Vertex v1 = new Vertex(bx, by, 0);
                    Vertex v2 = new Vertex(bx, by + ystep, 0);
                    Vertex v3 = new Vertex(bx + xstep, by + ystep, 0);
                    result.Add(new Triangle(v1, v2, v3));

                    // Make the second triangle
                    v1 = new Vertex(bx, by, 0);
                    v2 = new Vertex(bx + xstep, by, 0);
                    v3 = new Vertex(bx + xstep, by + ystep, 0);
                    result.Add(new Triangle(v1, v2, v3));
                    by = by + ystep;
                }
                bx = bx + xstep;
            }

            bx = 0;
            by = 0;
            int ycell = 0;

            // Now fill in the base to the mesh edges
            for (int x = 0; x < width - 1; x++)
            {
                // Make the base sides
                Vertex v1 = new Vertex(bx, by, 0);
                Vertex v2 = new Vertex(bx + xstep, by, 0);
                Vertex v3 = new Vertex(bx + xstep, by, (grid[x + 1, ycell] - lo) * heightScale + baseHeight);
                Vertex v4 = new Vertex(bx, by, (grid[x, ycell] - lo) * heightScale + baseHeight);
                result.Add(new Triangle(v1, v2, v3));
                result.Add(new Triangle(v1, v3, v4));
                bx = bx + xstep;
            }


            by = (depth - 1) * ystep;
            ycell = (int)depth - 1;
            bx = 0;
            //  Now fill in the base to the mesh edges
            for (int x = 0; x < width - 1; x++)
            {
                // Make the base sides
                Vertex v1 = new Vertex(bx, by, 0);
                Vertex v2 = new Vertex(bx + xstep, by, 0);
                Vertex v3 = new Vertex(bx + xstep, by, (grid[x + 1, ycell] - lo) * heightScale + baseHeight);
                Vertex v4 = new Vertex(bx, by, (grid[x, ycell] - lo) * heightScale + baseHeight);
                result.Add(new Triangle(v1, v2, v3));
                result.Add(new Triangle(v1, v3, v4));
                bx = bx + xstep;
            }

            by = 0;
            bx = 0;
            int xcell = 0;

            // Now fill in the base to the mesh edges
            for (int y = 0; y < depth - 1; y++)
            {
                // Make the base sides
                Vertex v1 = new Vertex(bx, by, 0);
                Vertex v2 = new Vertex(bx, by + ystep, 0);
                Vertex v3 = new Vertex(bx, by + ystep, (grid[xcell, y + 1] - lo) * heightScale + baseHeight);
                Vertex v4 = new Vertex(bx, by, (grid[xcell, y] - lo) * heightScale + baseHeight);
                result.Add(new Triangle(v1, v2, v3));
                result.Add(new Triangle(v1, v3, v4));
                by = by + ystep;
            }

            by = 0;
            bx = (width - 1) * xstep;
            xcell = (int)width - 1;
            // Now fill in the base to the mesh edges
            for (int y = 0; y < depth - 1; y++)
            {
                // Make the base sides
                Vertex v1 = new Vertex(bx, by, 0);
                Vertex v2 = new Vertex(bx, by + ystep, 0);
                Vertex v3 = new Vertex(bx, by + ystep, (grid[xcell, y + 1] - lo) * heightScale + baseHeight);
                Vertex v4 = new Vertex(bx, by, (grid[xcell, y] - lo) * heightScale + baseHeight);
                result.Add(new Triangle(v1, v2, v3));
                result.Add(new Triangle(v1, v3, v4));
                by = by + ystep;
            }

            // Now make the mesh

            bx = 0;
            for (int x = 0; x < width - 1; x++)
            {
                by = 0;
                for (int y = 0; y < depth-1; y++)
                {
                    // Make the first triangle
                    Vertex v1 = new Vertex(bx, by, (grid[x, y] - lo) * heightScale + baseHeight);
                    Vertex v2 = new Vertex(bx + xstep, by, (grid[x + 1, y] - lo) * heightScale + baseHeight);
                    Vertex v3 = new Vertex(bx + xstep, by + ystep, (grid[x + 1, y + 1] - lo) * heightScale + baseHeight);
                    Vertex v4 = new Vertex(bx, by + ystep, (grid[x, y + 1] - lo) * heightScale + baseHeight);
                    result.Add(new Triangle(v1, v2, v3));
                    result.Add(new Triangle(v1, v3, v4));
                    by = by + ystep;
                }
                bx = bx + xstep;
            }
            return result;
        }

        List<Triangle> MakeMesh(ushort[] image, int width, int depth, int modelWidth, int modelHeight)
        {
            int imagePos = 0;
            double[,] grid = new double[width, depth];


            for (int x = width-1; x > 0; x--)
            {
                for (int y = 0; y < depth; y++)
                {
                    grid[x, y] = image[imagePos];
                    imagePos++;
                }
            }

            // Now halve the resolution and remove any outliers

            // Make sure the height and width are even numbers
            if (width % 2 == 1) width--;
            if (depth % 2 == 1) depth--;

            double[,] filteredGrid = new double[width / 2, depth / 2];
            int filtx=0, filty=0;

            for (int x = 0; x < width-2; x+=2)
            {
                for (int y = 0; y < depth-2; y+=2)
                {
                    // Make an array of the values in the area we are averaging
                    double [] square = new double [] {grid[x, y],grid[x+1, y],grid[x, y+1],grid[x+1, y+1]};

                    // Work out their average
                    double total = 0;
                    foreach (double d in square)
                    {
                        total += d;
                    }
                    double firstMean = total / square.Length;

                    // Work out their standard deviation

                    double devTotal = 0;
                    foreach (double d in square)
                    {
                        double dev = d - firstMean;
                        devTotal += dev * dev;
                    }

                    double stdDev = Math.Sqrt(devTotal/square.Length);

                    // Make a new average ignoring outliers
                    double revisedTotal = 0;
                    int revisedCount = 0;

                    foreach (double d in square)
                    {
                        if (Math.Abs(d - firstMean) < stdDev) 
                        {
                            // Value looks OK - use it to work out the mean
                            revisedTotal += d;
                            revisedCount++;
                        }
                    }

                    double revisedMean ;

                    if (revisedCount>0)
                    {
                        // If we have enough to calculate a revised mean, do it
                        revisedMean = revisedTotal / revisedCount;
                    }
                    else
                    {
                        // If all the values are wacko we still need a value
                        revisedMean = firstMean;
                    }

                    filteredGrid[filtx, filty] = revisedMean;
                    filty++;
                }
                filtx++;
                filty = 0;
            }

            List<Triangle> result = SolidPlot(grid: filteredGrid, modelWidth: modelWidth, baseHeight: 1, modelHeight: modelHeight);
            return result;
        }

        /// <summary>
        /// Stores a linear array of height values as an STL mesh. 
        /// </summary>
        /// <param name="image">image data</param>
        /// <param name="width">width of the image in pixels</param>
        /// <param name="depth"></param>
        /// <param name="modelHeight"></param>
        /// <param name="filename"></param>
        public void StoreSTLMesh(ushort[] image, int width, int depth, int modelWidth, int modelHeight, string filename)
        {
            List<Triangle> mesh = MakeMesh(image, width, depth, modelWidth, modelHeight);
            WriteTriangles(mesh, filename);
        }

        void WriteTriangles(List<Triangle> grid, string filename)
        {
            using (BinaryWriter f = new BinaryWriter(File.Open(filename, FileMode.Create)))
            {

                // Write out the header - 80 spaces
                for (int i = 0; i < 80; i++)
                    f.Write((byte)' ');

                // Write out the number of triangles 
                f.Write((UInt32)grid.Count);

                // Write out the triangles
                foreach (Triangle t in grid)
                {
                    // Write out the normal
                    f.Write((float)0);
                    f.Write((float)0);
                    f.Write((float)0);
                    // Write out the vertex
                    t.STLWrite(f);
                    f.Write((UInt16)0);
                }

                f.Close();
            }
        }


        #endregion
        public MainWindow()
        {
            InitializeComponent();
            Setup();
        }

        private void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            takeSnapShot = true;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
@"The Far Cutoff slider sets the back plane of the picture
The Near Cutoff slider sets the front plane. 
Areas that are red (too close) or blue (too far away) are not rendered.
Areas in Grey Scale will be rendered.
Distances are in mm.

Press Take Picture to produce an STL file in your Documents folder.
The screen will flash if the picture has been successfully stored.
You can enter your own filenames.

To take a Selfie press the Take Selfie button. 
Set the delay in the box next to the button.

The ModelWidth slider sets the width of the model produced in mm.
The ModelHeight slider sets the height of the model, in mm.

Needs Kinect Version 2 in a USB 3 port.

www.robmiles.com
September 2014
", "Kinect 3D Camera");
        }


        # region Taking Selfies

        bool takeSelfie = false;
        DateTime selfieStartTime;
        int selfieDelayInSecs = 10;
        string selfieDelay;
        Brush idleBackgroundBrush;
        Brush activeBackgroundBrush = new SolidColorBrush(Colors.Red);

        void startSelfie()
        {
            try
            {
                // Check for the textbox first because if this throws an exception you 
                // don't want to go any further
                selfieDelayInSecs = Math.Abs(int.Parse(SelfieTimeTextBox.Text));
                idleBackgroundBrush = SelfieTimeTextBox.Background;
                SelfieTimeTextBox.Background = activeBackgroundBrush;
                selfieDelay = SelfieTimeTextBox.Text;
                selfieStartTime = DateTime.Now;
                takeSelfie = true;
            }
            catch
            {
                MessageBox.Show("Invalid delay value", "Take Selfie");
            }
        }

        void checkSelfie()
        {
            if(takeSelfie)
            {
                int timeLeft = selfieDelayInSecs - (int)Math.Round((DateTime.Now-selfieStartTime).TotalSeconds);
                if (timeLeft == 0)
                {
                    SelfieTimeTextBox.Text = selfieDelay;
                    SelfieTimeTextBox.Background = idleBackgroundBrush;
                    takeSelfie = false;
                    takeSnapShot = true;
                }
                else
                {
                    SelfieTimeTextBox.Text = timeLeft.ToString();
                }
            }

        }

        #endregion

        private void FarSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            FarCutoffTextBlock.Text = "Far Cutoff : " + Math.Round(FarSlider.Value).ToString();
        }

        private void NearSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            NearCutoffTextBlock.Text = "Near Cutoff : " + Math.Round(NearSlider.Value).ToString();
        }

        private void ModelHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PictureReliefTextBlock.Text = "Picture relief height : " + Math.Round(ModelHeightSlider.Value).ToString();
        }

        private void ModelWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PictureWidthTextBlock.Text = "Picture width : " + Math.Round(ModelWidthSlider.Value).ToString();
        }

        private void SelfieButton_Click(object sender, RoutedEventArgs e)
        {
            startSelfie();
        }
    }
}
