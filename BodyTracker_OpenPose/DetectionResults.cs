using OpenPoseDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyTracker_OpenPose
{
    public class DetectionResults // contain frame detection data
    {
        public Datum data; // keypoint data
        public Boolean isFinished; // finished flag - when set to true, indicates that there are no more frames left
    }
}
