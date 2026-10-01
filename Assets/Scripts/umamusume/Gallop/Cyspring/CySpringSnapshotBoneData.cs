using UnityEngine;

namespace Gallop
{
    public class CySpringSnapshotBoneData
    {
        public string BoneName;
        public Vector3 TargetPosition;
        public Vector3 PrevTargetPosition;
        public Vector3 SelfPosition;
        public Vector3 AimVector;

        public Quaternion FinalRotation;
        public Quaternion ParentRotation;
        public Quaternion LocalRotation;

        public Vector3 Position
        {
            get { return TargetPosition; }
            set { TargetPosition = value; }
        }

        public Quaternion Rotation
        {
            get { return FinalRotation; }
            set { FinalRotation = value; }
        }

        public CySpringSnapshotBoneData()
        {
            FinalRotation = Quaternion.identity;
            ParentRotation = Quaternion.identity;
            LocalRotation = Quaternion.identity;
        }
    }
}
