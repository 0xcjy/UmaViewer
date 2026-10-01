using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    [Serializable]
    public class CySpringParamDataChildElement
    {
        public const float InitialLimitAngle = 180f;

        public string _boneName;

        public float _stiffnessForce;
        public float _dragForce;
        public float _gravity;
        public float _collisionRadius;

        public bool _needEnvCollision;

        public List<string> _collisionNameList;

        public float _verticalWindRateSlow;
        public float _horizontalWindRateSlow;
        public float _verticalWindRateFast;
        public float _horizontalWindRateFast;

        public bool _isLimit;
        public Vector3 _limitAngleMin;
        public Vector3 _limitAngleMax;

        public float MoveSpringApplyRate;

        // ===== official-style accessors / aliases =====

        public string Name
        {
            get { return _boneName; }
        }

        public string BoneName
        {
            get { return _boneName; }
        }

        public float StiffnessForce
        {
            get { return _stiffnessForce; }
        }

        public float DragForce
        {
            get { return _dragForce; }
        }

        public float Gravity
        {
            get { return _gravity; }
        }

        public float CollisionRadius
        {
            get { return _collisionRadius; }
        }

        public bool NeedEnvCollision
        {
            get { return _needEnvCollision; }
        }

        public bool CheckEnvCollision
        {
            get { return _needEnvCollision; }
        }

        public List<string> CollisionNameList
        {
            get
            {
                if (_collisionNameList == null)
                    _collisionNameList = new List<string>();

                return _collisionNameList;
            }
        }

        public float VerticalWindRateSlow
        {
            get { return _verticalWindRateSlow; }
        }

        public float HorizontalWindRateSlow
        {
            get { return _horizontalWindRateSlow; }
        }

        public float VerticalWindRateFast
        {
            get { return _verticalWindRateFast; }
        }

        public float HorizontalWindRateFast
        {
            get { return _horizontalWindRateFast; }
        }

        public bool IsLimit
        {
            get { return _isLimit; }
        }

        public Vector3 LimitAngleMin
        {
            get { return _limitAngleMin; }
        }

        public Vector3 LimitAngleMax
        {
            get { return _limitAngleMax; }
        }

        public Vector3 LimitRotationMin
        {
            get { return _limitAngleMin; }
        }

        public Vector3 LimitRotationMax
        {
            get { return _limitAngleMax; }
        }

        public CySpringParamDataChildElement()
        {
            _boneName = string.Empty;

            _stiffnessForce = 0f;
            _dragForce = 0f;
            _gravity = 0f;
            _collisionRadius = 0f;

            _needEnvCollision = false;

            _collisionNameList = new List<string>();

            _verticalWindRateSlow = 1f;
            _horizontalWindRateSlow = 1f;
            _verticalWindRateFast = 1f;
            _horizontalWindRateFast = 1f;

            _isLimit = false;
            _limitAngleMin = new Vector3(InitialLimitAngle, InitialLimitAngle, InitialLimitAngle);
            _limitAngleMax = new Vector3(InitialLimitAngle, InitialLimitAngle, InitialLimitAngle);

            MoveSpringApplyRate = 1f;
        }
    }
}