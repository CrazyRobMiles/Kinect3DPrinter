Kinect3DPrinter
===============

You know that bit at the end of Star Wars 2/5 when Han Solo gets frozen into Carbonite?

Well, you can use this program to do the same to yourself.

It takes the depth image from a Kinect 2 sensor and creates an STL object for 3D printing.

You can set the near and far planes to clip the depth image and also the width and height of the object that is to be produced.

You can also control the amount of averaging that is performed on the frames. Increasing averaging can bring out more detail (particularly if the subject is close to the sensor) but the display will update more slowly and the subject must keep still.

The STL file is stored in your Documents folder.

It can be loaded into any slicing program that you fancy (I use Cura) and used to produce interesting prints.
