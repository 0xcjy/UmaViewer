using UnityEngine;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// 按 controller 的原版消费方式改成扁平结构：
    /// controller 直接从这里拷贝颜色 / power / offset / speed / elapsed / texture / enable。
    ///
    /// 为了兼容你现在的 driver 还保留了 data，便于用 data.name 找目标材质。
    /// 如果你后面把原版 stage 绑定链完全补齐，可以再把 data 从这个 struct 挪走。
    /// </summary>
    public struct UVScrollLightUpdateInfo
    {
        public LiveTimelineUVScrollLightData data;

        public float elapsedTime;
        public Color mulColor0;
        public Color mulColor1;
        public float colorPower;
        public float scrollOffsetX;
        public float scrollOffsetY;
        public float scrollSpeedX;
        public float scrollSpeedY;
        public Texture2D texture;
        public bool isEnabledTexture;

        /// <summary>
        /// 兼容 helper：
        /// 你现在如果 timeline 侧还是先拿 key，再组事件，
        /// 可以直接用这个方法把 key 打平成 controller 真正需要的 updateInfo。
        /// </summary>
        public static UVScrollLightUpdateInfo Create(
            LiveTimelineUVScrollLightData data,
            LiveTimelineKeyUVScrollLightData key,
            float elapsedTime)
        {
            UVScrollLightUpdateInfo info = new UVScrollLightUpdateInfo
            {
                data = data,
                elapsedTime = elapsedTime,
                mulColor0 = key != null ? key.mulColor0 : Color.white,
                mulColor1 = key != null ? key.mulColor1 : Color.white,
                colorPower = key != null ? key.colorPower : 1f,
                scrollOffsetX = key != null ? key.scrollOffsetX : 0f,
                scrollOffsetY = key != null ? key.scrollOffsetY : 0f,
                scrollSpeedX = key != null ? key.scrollSpeedX : 0f,
                scrollSpeedY = key != null ? key.scrollSpeedY : 0f,
                texture = key != null ? key.texture : null,
                isEnabledTexture = key != null && key.IsEnabledTexture,
            };
            return info;
        }
    }

    public delegate void UVScrollLightUpdateInfoDelegate(ref UVScrollLightUpdateInfo updateInfo);
}
