using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// CySpring collision authoring data.
    ///
    /// Reconstructed from CySpringCollisionData dummy + IDA:
    /// - constructor stores serialized config fields
    /// - Create resolves the target Transform and creates a child GameObject named CollisionName
    /// - RuntimeData owns the actual native-collision update/scaling logic
    /// </summary>
    [Serializable]
    public class CySpringCollisionData
    {
        private const string None = "None";

        [SerializeField] public string _collisionName;
        [SerializeField] public string _targetObjectName;
        [SerializeField] public bool _isOtherTarget;
        [SerializeField] public Vector3 _offset;
        [SerializeField] public Vector3 _offset2;
        [SerializeField] public float _radius;
        [SerializeField] public float _distance;
        [SerializeField] public Vector3 _normal;
        [SerializeField] public CollisionType _type;
        [SerializeField] public bool _isInner;

        private CySpringCollisionRuntimeData _runtimeData;

        public string CollisionName
        {
            get => _collisionName;
            set => _collisionName = value;
        }

        public CySpringCollisionRuntimeData RuntimeData => _runtimeData;

        public CySpringCollisionData() { }

        public CySpringCollisionData(
            string collisionName,
            string targetObjectName,
            Vector3 offset,
            float radius,
            float distance,
            Vector3 normal)
            : this(
                CollisionType.Sphere,
                false,
                collisionName,
                targetObjectName,
                false,
                offset,
                Vector3.zero,
                radius,
                distance,
                normal)
        {
        }

        public CySpringCollisionData(
            CollisionType type,
            bool isInner,
            string collisionName,
            string targetObjectName,
            bool isOtherTarget,
            Vector3 offset,
            Vector3 offset2,
            float radius,
            float distance,
            Vector3 normal)
        {
            _type = type;
            _isInner = isInner;
            _collisionName = collisionName;
            _targetObjectName = targetObjectName;
            _isOtherTarget = isOtherTarget;
            _offset = offset;
            _offset2 = offset2;
            _radius = radius;
            _distance = distance;
            _normal = normal;
        }

        public void Init(float legacyScale)
        {
            // Official Init only forwards to RuntimeData.ScaleParams.
            if (_runtimeData != null)
                _runtimeData.ScaleParams(legacyScale);
        }

        public bool Create(
            CySpringCollision root,
            Dictionary<string, Transform> transformCacheDic,
            FindTransformAction otherTransformAction)
        {
            if (root == null)
                return false;

            if (string.IsNullOrEmpty(_collisionName) || string.Equals(_collisionName, None, StringComparison.Ordinal))
                return false;

            if (string.IsNullOrEmpty(_targetObjectName) || string.Equals(_targetObjectName, None, StringComparison.Ordinal))
                return false;

            if (_radius == 0.0f)
                return false;

            GameObject targetGameObject = ResolveTargetGameObject(root, transformCacheDic, otherTransformAction);
            if (targetGameObject == null)
                return false;

            GameObject collisionObject = new GameObject(_collisionName);

            _runtimeData = new CySpringCollisionRuntimeData
            {
                CollisionType = _type,
                IsInner = _isInner,
                Radius = _radius,
                Offset = _offset,
                Offset2 = _offset2,
                Name = _collisionName
            };
            _runtimeData.Initialize(collisionObject.transform);

            Transform collisionTransform = collisionObject.transform;
            collisionTransform.SetParent(targetGameObject.transform, false);

            if (_type == CollisionType.Sphere)
            {
                // Official sphere branch: after parenting, world position += offset.
                collisionTransform.position = collisionTransform.position + _offset;
            }
            else if (_type == CollisionType.Plane)
            {
                _runtimeData.SetTransform(_distance, _normal);
            }

            return true;
        }

        private GameObject ResolveTargetGameObject(
            CySpringCollision root,
            Dictionary<string, Transform> transformCacheDic,
            FindTransformAction otherTransformAction)
        {
            if (_isOtherTarget && otherTransformAction != null)
            {
                if (otherTransformAction(_targetObjectName, out Transform otherTransform) && otherTransform != null)
                    return otherTransform.gameObject;

                return null;
            }

            if (transformCacheDic != null)
            {
                if (transformCacheDic.TryGetValue(_targetObjectName, out Transform cachedTransform) && cachedTransform != null)
                    return cachedTransform.gameObject;

                return null;
            }

            GameObject rootObject = GetRootGameObject(root);
            return rootObject != null ? CySpring.FindGameObject(rootObject, _targetObjectName) : null;
        }

        private static GameObject GetRootGameObject(CySpringCollision root)
        {
            if (root == null)
                return null;

            return root.RootObject;
        }

        public void Delete()
        {
            if (_runtimeData != null && _runtimeData.TargetTransform != null)
            {
                GameObject go = _runtimeData.TargetTransform.gameObject;
                if (go != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEngine.Object.DestroyImmediate(go);
                    else
#endif
                        UnityEngine.Object.Destroy(go);
                }
            }

            _runtimeData = null;
        }

        public enum CollisionType
        {
            Sphere,
            None,
            Capsule,
            Plane
        }

        public delegate bool FindTransformAction(string key, out Transform transform);
    }
}
