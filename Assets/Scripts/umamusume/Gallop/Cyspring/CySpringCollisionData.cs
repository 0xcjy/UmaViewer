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

        [SerializeField] private string _collisionName;
        [SerializeField] private string _targetObjectName;
        [SerializeField] private bool _isOtherTarget;
        [SerializeField] private Vector3 _offset;
        [SerializeField] private Vector3 _offset2;
        [SerializeField] private float _radius;
        [SerializeField] private float _distance;
        [SerializeField] private Vector3 _normal;
        [SerializeField] private CollisionType _type;
        [SerializeField] private bool _isInner;

        private CySpringCollisionRuntimeData _runtimeData;

        public string CollisionName
        {
            get => _collisionName;
            set => _collisionName = value;
        }

        //(P1 — port del camino ORIGINAL de física) Los campos son privados porque los consume
        //el puente nativo (CySpringController). El camino original (DynamicBone) los leía
        //directamente como públicos. Estos accesores de solo lectura permiten recuperar ese
        //camino sin abrir los campos ni tocar la serialización.
        public string TargetObjectName => _targetObjectName;
        public bool IsOtherTarget => _isOtherTarget;
        public Vector3 Offset => _offset;
        public Vector3 Offset2 => _offset2;
        public float Radius => _radius;

        // (INTENTO 3b) Permite ajustar el radio desde el cargador (p. ej. anular un colisor
        // concreto antes del bind). El pinchazo del puente nativo construye el array de colisiones
        // desde este dato, asi que el cambio debe hacerse ANTES de LoadFromDataContainers.
        public void SetRadius(float radius)
        {
            _radius = radius;
        }
        public float Distance => _distance;
        public Vector3 Normal => _normal;
        public CollisionType Type => _type;
        public bool IsInner => _isInner;

        public CySpringCollisionRuntimeData RuntimeData => _runtimeData;

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
