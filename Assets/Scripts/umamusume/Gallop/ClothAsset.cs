using UnityEngine;

namespace Gallop
{
    /// <summary>
    // 角色布料资源管理
    // 用于保存不同身体部位、不同布料类别对应的 GameObject 预制体
    // 第一维表示布料部位,第二维表示布料类别
    /// </summary>
    
    public class ClothAsset
    {
        public const int ClothPartsNum = 4;
        private const int ClothCategoryNum = 5;

        private CySpringDataContainer.Category _clothCategory;
        private GameObject[][] _clothAsset;

        public CySpringDataContainer.Category ClothCategory
        {
            get
            {
                return _clothCategory;
            }
            set
            {
                _clothCategory = value;
            }
        }

        public GameObject HeadClothAsset
        {
            get
            {
                return _clothAsset[(int)ClothParts.Head][(int)_clothCategory];
            }
        }

        public GameObject BodyClothAsset
        {
            get
            {
                return _clothAsset[(int)ClothParts.Body][(int)_clothCategory];
            }
        }

        public GameObject TailClothAsset
        {
            get
            {
                return _clothAsset[(int)ClothParts.Tail][(int)_clothCategory];
            }
        }

        public GameObject BustClothAsset
        {
            get
            {
                return _clothAsset[(int)ClothParts.Bust][(int)_clothCategory];
            }
        }

        public ClothAsset()
        {
            _clothCategory = (CySpringDataContainer.Category)2;
        }

        public void InitClothData()
        {
            _clothAsset = new GameObject[ClothPartsNum][];

            _clothAsset[(int)ClothParts.Head] = new GameObject[ClothCategoryNum];
            _clothAsset[(int)ClothParts.Body] = new GameObject[ClothCategoryNum];
            _clothAsset[(int)ClothParts.Tail] = new GameObject[ClothCategoryNum];
            _clothAsset[(int)ClothParts.Bust] = new GameObject[ClothCategoryNum];
        }

        public void ImportHeadClothData(GameObject prefab, int category)
        {
            if (_clothAsset != null)
                _clothAsset[(int)ClothParts.Head][category] = prefab;
        }

        public void ImportBodyClothData(GameObject prefab, int category)
        {
            if (_clothAsset != null)
                _clothAsset[(int)ClothParts.Body][category] = prefab;
        }

        public void ImportTailClothData(GameObject prefab, int category)
        {
            if (_clothAsset != null)
                _clothAsset[(int)ClothParts.Tail][category] = prefab;
        }

        public void ImportBustClothData(GameObject prefab, int category)
        {
            if (_clothAsset != null)
                _clothAsset[(int)ClothParts.Bust][category] = prefab;
        }

        public enum ClothParts
        {
            Head,
            Body,
            Tail,
            Bust
        }
    }
}