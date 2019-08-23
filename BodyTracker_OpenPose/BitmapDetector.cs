using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenPoseDotNet;

using UserDatum = OpenPoseDotNet.CustomDatum;

namespace BodyTracker_OpenPose
{
    class BitmapDetector : IDisposable
    {
        private Wrapper<UserDatum> opWrapper; // openpose wrapper

        // data objects used to process results
        private StdSharedPtr<StdVector<StdSharedPtr<CustomDatum>>> datumProcessed;
        private StdVector<StdSharedPtr<CustomDatum>> data;

        /* 
         * initializer for bitmap input
         * netRes = net resolution
         * faceRes = face detector resolution, disable by setting to null
         * handRes = hand detector resolution, disable by setting to null
         * modelPose = which pose model to use
         * outPath = output path for frames, set null to disable output, disable by setting to null
        */
        public BitmapDetector(string netRes, string faceRes, string handRes, string modelPose, string outPath)
        {
            Detector.InitFlags(netRes, faceRes, handRes, modelPose, outPath); // initializes some flag values with input

            opWrapper = new Wrapper<UserDatum>(ThreadManagerMode.Asynchronous); // create openpose wrappper, setting to thread mode to async out because of our cutom method of output

            // configure openpose wrapper obj based on the flags that we set
            Detector.ConfigOnFlags(opWrapper, 
                false); // don't set input on config, we're inputting a bitmap on method call
        }
        public Datum Run(Bitmap image) // detect on bitmap object
        {
            if(image != null) // make sure image exists
            {
                return ProcessBitmap(image);
            }
            return null;

        }
        private Datum ProcessBitmap(Bitmap bmp)
        {
            BitmapData bmpData = null;
            try
            {
                var width = bmp.Width;
                var height = bmp.Height;
                bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                var stride = bmpData.Stride;
                var scan0 = bmpData.Scan0;

                unsafe
                {
                    // convert bitmap to byte array
                    var line = width * 3;
                    var image = new byte[line * height];
                    var ps = (byte*)scan0;
                    for (var h = 0; h < height; h++)
                    {
                        Marshal.Copy((IntPtr)ps, image, line * h, line);
                        ps += stride;
                    }

                    // use openpose wrapper to calculate keypoints using byte array object as input image
                    this.datumProcessed = opWrapper.EmplaceAndPop(image, width, height, MatType.CV_8UC3);

                    if (this.datumProcessed != null) // if output data exists
                    {
                        if (this.datumProcessed != null && this.datumProcessed.TryGet(out this.data) && !this.data.Empty) // if datumProcessed exists && we can get the data sucessfully && retrieved data exists
                        {
                            return this.data.ToArray()[0].Get(); // retrieve datum object which contains the keypoint data
                        }
                        else // bad input
                        {
                            OpenPose.Log("Image could not be processed.", Priority.High);
                        }
                    }

                }
            }
            finally
            {
                if (bmpData != null)
                    bmp.UnlockBits(bmpData);
            }
            return null;
        }

        // method disposes data that was used during processing
        // call after we're done using the keypoint data
        public void DisposeData()
        {
            // check if objects exist and aren't disposed already
            // if so, dispose them
            if (datumProcessed != null && !datumProcessed.IsDisposed)
                datumProcessed.Dispose();
            if (data != null && !data.IsDisposed)
                data.Dispose();
        }

        // dispose of openpose wrapper
        public void Dispose()
        {
            if(opWrapper != null && !opWrapper.IsDisposed) // if wrapper exists and has not already been disposed
                opWrapper.Dispose();
        }
    }
}
