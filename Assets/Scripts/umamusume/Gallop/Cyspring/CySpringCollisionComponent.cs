using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// Colisor de CySpring. Es el puente entre los datos de autoria (radio, tipo, offsets, filtros)
    /// y el <see cref="CySpringCollisionRuntimeData"/> que lee el solver nativo.
    ///
    /// El cutt crea uno por cada entrada de la pista de colisiones de entorno (EnvCollision): el
    /// objeto se instancia bajo el Director y la key del timeline le escribe posicion, rotacion y
    /// radio cada frame.
    /// </summary>
    public class CySpringCollisionComponent : MonoBehaviour
    {
        [SerializeField] protected float _radius;
        [SerializeField] protected CySpringCollisionData.CollisionType _collisionType;
        [SerializeField] protected bool _isInner;
        protected Vector3 _offset;
        protected Vector3 _offset2;
        protected bool _filterSpiringNode;
        protected bool _filterSpiringNodeExclusive;
        protected HashSet<CySpringBoneBase> _filterSpringHashSet;
        protected CySpringCollisionRuntimeData _runtimeData;

        public bool IsInitializeRuntimeData => _runtimeData != null;

        public CySpringCollisionRuntimeData RuntimeData => _runtimeData;

        public float Radius
        {
            get => _radius;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.Radius = value;
                _runtimeData.SetDefaultRadius(value);
                _radius = value;
            }
        }

        public CySpringCollisionData.CollisionType CollisionType
        {
            get => _collisionType;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.CollisionType = value;
                _collisionType = value;
            }
        }

        public bool IsInner
        {
            get => _isInner;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.IsInner = value;
                _isInner = value;
            }
        }

        public Vector3 Offset
        {
            get => _offset;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.Offset = value;
                _runtimeData.SetDefaultOffset(value);
                _offset = value;
            }
        }

        public Vector3 Offset2
        {
            get => _offset2;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.Offset2 = value;
                _runtimeData.SetDefaultOffset2(value);
                _offset2 = value;
            }
        }

        public bool FilterSpiringNode
        {
            get => _filterSpiringNode;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.FilterSpringNode = value;
                _filterSpiringNode = value;
            }
        }

        public bool FilterSpiringNodeExclusive
        {
            get => _filterSpiringNodeExclusive;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.FilterSpringNodeExclusive = value;
                _filterSpiringNodeExclusive = value;
            }
        }

        public HashSet<CySpringBoneBase> FilterSpringHashSet
        {
            get => _filterSpringHashSet;
            set
            {
                if (_runtimeData == null)
                    throw new System.NullReferenceException();

                _runtimeData.FilterSpringHashSet = value;
                _filterSpringHashSet = value;
            }
        }

        public float Distance => _runtimeData != null
            ? _runtimeData.Distance
            : throw new System.NullReferenceException();

        public Vector3 Normal => _runtimeData != null
            ? _runtimeData.Normal
            : throw new System.NullReferenceException();

        public Transform CacheTransform => _runtimeData != null
            ? _runtimeData.TargetTransform
            : throw new System.NullReferenceException();

        /// <summary>
        /// Indica si el colisor vive en el espacio del mundo (colisiones de entorno) en lugar de
        /// colgar de un hueso del personaje.
        /// </summary>
        public virtual bool IsWorldCollision()
        {
            return false;
        }

        /// <summary>
        /// Crea el runtime data a partir de los valores del componente, o adopta uno existente.
        /// </summary>
        public void Initialize(CySpringCollisionRuntimeData runtimeData = null)
        {
            if (runtimeData == null)
            {
                _runtimeData = new CySpringCollisionRuntimeData
                {
                    CollisionType = _collisionType,
                    IsInner = _isInner,
                    Radius = _radius,
                    Offset = _offset,
                    Offset2 = _offset2,
                    FilterSpringNode = _filterSpiringNode,
                    FilterSpringNodeExclusive = _filterSpiringNodeExclusive,
                    FilterSpringHashSet = _filterSpringHashSet
                };

                _runtimeData.Initialize(transform);
                _runtimeData.IsWorld = IsWorldCollision();
                return;
            }

            _runtimeData = runtimeData;
            _offset = runtimeData.Offset;
            _offset2 = runtimeData.Offset2;
            _radius = runtimeData.Radius;
            _collisionType = runtimeData.CollisionType;
            _isInner = runtimeData.IsInner;
            _filterSpiringNode = runtimeData.FilterSpringNode;
            _filterSpiringNodeExclusive = runtimeData.FilterSpringNodeExclusive;
            _filterSpringHashSet = runtimeData.FilterSpringHashSet;
        }
    }
}
