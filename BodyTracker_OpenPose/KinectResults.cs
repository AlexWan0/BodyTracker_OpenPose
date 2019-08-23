using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenPoseDotNet;
using System.Drawing;

namespace BodyTracker_OpenPose
{
    class KinectResults // used by the kinect detection loop to transfer image/keypoint data
    {
        // kinect images
        public Bitmap colorImage; 
        public Bitmap depthImage;

        // openpose keypoint data
        public Datum poseData;
    }
}
