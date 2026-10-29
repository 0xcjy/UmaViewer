using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Key de la pista de colisiones de entorno del cutt (EnvCollision). Describe un colisor
    /// esferico/capsula/plano que se coloca en la escena para que el pelo, la ropa y la cola
    /// choquen con el (suelo, atrezzo, etc.), no con el propio personaje.
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyCharaCollisionData : LiveTimelineKeyWithInterpolate
    {
        /// <summary>
        /// Giro que convierte la rotacion de la key en una normal "hacia arriba": el forward del
        /// transform del colisor plano apunta al eje Y del mundo.
        /// </summary>
        public static readonly Quaternion PLANE_UP_ROTATION = Quaternion.Euler(-90f, 0f, 0f);

        private const int ATTR_PLANE_UP = 65536;

        public bool IsEnable;
        public Vector3 Position;
        public Vector3 Angle;
        public CySpringCollisionData.CollisionType CollisionType = CySpringCollisionData.CollisionType.Plane;
        public float Radius;
        public Vector3 Offset;
        public Vector3 Offset2;
        public bool IsInner;
        public bool IsApplyHead = true;
        public bool IsApplyTail = true;
        public LiveCharaPositionFlag CharacterFlag;
        public Quaternion Rotation = Quaternion.identity;

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaCollision;

        /// <summary>
        /// Atributo de la key que marca el colisor plano como "plano hacia arriba" (suelo).
        /// Se guarda en el bit 16 del atributo, igual que en el juego.
        /// </summary>
        public bool IsEnabledPlaneUp
        {
            get => ((int)attribute & ATTR_PLANE_UP) != 0;
            set
            {
                int bits = (int)attribute & ~ATTR_PLANE_UP;
                if (value)
                    bits |= ATTR_PLANE_UP;

                attribute = (LiveTimelineKeyAttribute)bits;
            }
        }

        public override void OnLoad(LiveTimelineControl timelineControl)
        {
            // El bundle solo guarda los angulos de Euler; la rotacion se deriva al cargar.
            Rotation = Quaternion.Euler(Angle);
        }
    }

    [Serializable]
    public class LiveTimelineKeyCharaCollisionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaCollisionData> { }

    [Serializable]
    public class LiveTimelineCharaCollisionData : ILiveTimelineGroupDataWithName
    {
        private const string DEFAULT_NAME = "EnvCollision";

        public LiveTimelineKeyCharaCollisionDataList keys = new LiveTimelineKeyCharaCollisionDataList();

        public override ILiveTimelineKeyDataList GetKeyList() => keys;

        public LiveTimelineCharaCollisionData() : base(DEFAULT_NAME)
        {
        }
    }
}
