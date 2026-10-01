using UnityEngine;

namespace Gallop
{
    public abstract class CySpringOwner
    {
        private ClothAsset _clothAsset;

        public GameObject HeadClothAsset
        {
            get
            {
                return _clothAsset.HeadClothAsset;
            }
        }

        public GameObject BustClothAsset
        {
            get
            {
                return _clothAsset.BustClothAsset;
            }
        }

        public GameObject BodyClothAsset
        {
            get
            {
                return _clothAsset.BodyClothAsset;
            }
        }

        public GameObject TailClothAsset
        {
            get
            {
                return _clothAsset.TailClothAsset;
            }
        }

        public ClothAsset ClothAsset
        {
            get
            {
                return _clothAsset;
            }
        }

        public CySpringOwner(ClothAsset clothAsset)
        {
            _clothAsset = clothAsset;
        }

        public abstract bool IsCharaModel();

        public abstract int GetCharaID();

        public abstract int GetDressID();

        public abstract int GetHeadID();

        public abstract float GetCySpringCorrectScale(bool isHead);

        public abstract float GetBodyScale();

        public abstract float GetTotalScale();

        public virtual CySpringCollisionData.FindTransformAction GetOtherCharaTransformFindAction()
        {
            return null;
        }
    }
}