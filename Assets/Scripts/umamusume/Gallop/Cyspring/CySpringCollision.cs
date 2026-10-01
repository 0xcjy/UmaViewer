using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// CySpring collision owner reconstructed from CySpringCollision dummy + IDA.
    ///
    /// Official flow:
    /// Create(mainData, addData)
    ///   - _collisionData = mainData
    ///   - _collisionAddData = addData
    ///   - _runtimeDataList = new List<CySpringCollisionRuntimeData>(mainData._dataList.Count)
    ///   - create all main collisions
    ///   - create add collisions only if CollisionName does not exist in main data
    /// </summary>
    public class CySpringCollision
    {
        private GameObject _rootObject;
        private CySpringCollisionDataAsset _collisionData;
        private CySpringCollisionDataAsset _collisionAddData;
        private List<CySpringCollisionRuntimeData> _runtimeDataList;

        public GameObject RootObject
        {
            get { return _rootObject; }
        }

        public List<CySpringCollisionRuntimeData> RuntimeDataList
        {
            get { return _runtimeDataList; }
        }

        public CySpringCollision(GameObject rootObject)
        {
            _rootObject = rootObject;
            _runtimeDataList = new List<CySpringCollisionRuntimeData>();
        }

        public CySpringCollisionDataAsset GetData()
        {
            return _collisionData;
        }

        public CySpringCollisionDataAsset GetAddData()
        {
            return _collisionAddData;
        }

        public void Create(
            CySpringCollisionDataAsset data,
            CySpringCollisionDataAsset addData,
            float legacyScale,
            float addDataLegacyScale,
            Dictionary<string, Transform> transformCacheDic,
            CySpringCollisionData.FindTransformAction otherTransformFindAction)
        {
            _collisionData = data;
            _collisionAddData = addData;

            if (_collisionData == null || _collisionData._dataList == null)
                return;

            List<CySpringCollisionData> mainList = _collisionData._dataList;
            int mainCount = mainList.Count;

            _runtimeDataList = new List<CySpringCollisionRuntimeData>(mainCount);

            for (int i = 0; i < mainCount; i++)
            {
                CySpringCollisionData obj = mainList[i];
                if (obj == null)
                    continue;

                if (obj.Create(this, transformCacheDic, otherTransformFindAction))
                {
                    if (obj.RuntimeData != null)
                    {
                        _runtimeDataList.Add(obj.RuntimeData);
                        obj.RuntimeData.ScaleParams(legacyScale);
                    }
                }
            }

            if (_collisionAddData == null || _collisionAddData._dataList == null)
                return;

            List<CySpringCollisionData> addList = _collisionAddData._dataList;
            int addCount = addList.Count;

            for (int i = 0; i < addCount; i++)
            {
                CySpringCollisionData addObj = addList[i];
                if (addObj == null)
                    continue;

                string addName = addObj.CollisionName;
                bool existsInMain = false;

                for (int j = 0; j < mainCount; j++)
                {
                    CySpringCollisionData mainObj = mainList[j];
                    if (mainObj != null && string.Equals(addName, mainObj.CollisionName, StringComparison.Ordinal))
                    {
                        existsInMain = true;
                        break;
                    }
                }

                if (existsInMain)
                    continue;

                CreateObj(addObj, addDataLegacyScale, transformCacheDic, otherTransformFindAction);
            }
        }

        private void CreateObj(
            CySpringCollisionData obj,
            float legacyScale,
            Dictionary<string, Transform> transformCacheDic,
            CySpringCollisionData.FindTransformAction otherTransformFindAction)
        {
            if (obj == null)
                return;

            if (!obj.Create(this, transformCacheDic, otherTransformFindAction))
                return;

            if (_runtimeDataList == null)
                _runtimeDataList = new List<CySpringCollisionRuntimeData>();

            if (obj.RuntimeData != null)
            {
                _runtimeDataList.Add(obj.RuntimeData);
                obj.RuntimeData.ScaleParams(legacyScale);
            }
        }

        public void Delete()
        {
            if (_collisionData != null && _collisionData._dataList != null)
            {
                List<CySpringCollisionData> mainList = _collisionData._dataList;
                for (int i = 0; i < mainList.Count; i++)
                {
                    if (mainList[i] != null)
                        mainList[i].Delete();
                }
            }

            if (_collisionAddData != null && _collisionAddData._dataList != null)
            {
                List<CySpringCollisionData> addList = _collisionAddData._dataList;
                for (int i = 0; i < addList.Count; i++)
                {
                    if (addList[i] != null)
                        addList[i].Delete();
                }
            }

            if (_runtimeDataList != null)
                _runtimeDataList.Clear();
        }

        public void SetScale(float scale, float addDataScale)
        {
            List<CySpringCollisionData> mainList =
                (_collisionData != null) ? _collisionData._dataList : null;

            if (mainList != null)
            {
                for (int i = 0; i < mainList.Count; i++)
                {
                    CySpringCollisionData obj = mainList[i];
                    if (obj != null && obj.RuntimeData != null)
                        obj.RuntimeData.ScaleParams(scale);
                }
            }

            List<CySpringCollisionData> addList =
                (_collisionAddData != null) ? _collisionAddData._dataList : null;

            if (addList != null)
            {
                int mainCount = mainList != null ? mainList.Count : 0;

                for (int i = 0; i < addList.Count; i++)
                {
                    CySpringCollisionData addObj = addList[i];
                    if (addObj == null || addObj.RuntimeData == null)
                        continue;

                    bool existsInMain = false;
                    for (int j = 0; j < mainCount; j++)
                    {
                        CySpringCollisionData mainObj = mainList[j];
                        if (mainObj != null && string.Equals(addObj.CollisionName, mainObj.CollisionName, StringComparison.Ordinal))
                        {
                            existsInMain = true;
                            break;
                        }
                    }

                    if (!existsInMain)
                        addObj.RuntimeData.ScaleParams(addDataScale);
                }
            }
        }
    }
}
