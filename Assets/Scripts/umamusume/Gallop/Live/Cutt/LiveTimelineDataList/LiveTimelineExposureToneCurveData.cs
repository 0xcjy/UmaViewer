using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // 子系统 A7b（Exposure/ToneCurve 轨道）。
    // 字段名与偏移取自 umamusume.dll 反汇编 TypeTree：
    //   LiveTimelineKeyExposureData  (Gallop_Live_disasm.txt:118133-118139)
    //     IsEnable(bool,+48) DepthMask(f32,+52) Gain(f32,+56)
    //     Lift(f32,+60) MaskGain(f32,+64) MaskLift(f32,+68)
    //   LiveTimelineKeyToneCurveData (Gallop_Live_disasm.txt:120274-120282)
    //     IsEnable(bool,+48) DepthMask(f32,+52) ToneAnimationCurve(+56)
    //     MinCorrectionLevel(+64) MaxCorrectionLevel(+80)
    //     MaskToneCurve(+96) MaskMinCorrectionLevel(+104) MaskMaxCorrectionLevel(+120)
    // 原生 key 层还带通用帧插值头（interpolateType/curve/easingType）——
    // AlterUpdate_Exposure(0x1acfde0)/AlterUpdate_ToneCurve(0x1adace0) 均先调
    // CalculateInterpolationValue(0x1ade000) 再逐字段 Lerp，因此继承
    // LiveTimelineKeyWithInterpolate（与 LiveTimelineKeyRadialBlurData.cs 同型）。
    [Serializable]
    public class LiveTimelineKeyExposureData : LiveTimelineKeyWithInterpolate
    {
        // 原生 get_dataType 返回 0x65（LiveTimelineKeyExposureData.get_dataType
        // RVA=0x1a03280: mov eax,65h），对应 LiveTimelineKeyDataType.Exposure=101。
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Exposure;
        public bool IsEnable;
        public float DepthMask;
        public float Gain;
        public float Lift;
        public float MaskGain;
        public float MaskLift;
    }

    [Serializable]
    public class LiveTimelineKeyExposureDataList :
        LiveTimelineKeyDataListTemplate<LiveTimelineKeyExposureData> { }

    [Serializable]
    public class LiveTimelineKeyToneCurveData : LiveTimelineKeyWithInterpolate
    {
        // 原生 get_dataType 返回 0x64（LiveTimelineKeyToneCurveData.get_dataType
        // RVA=0x5220d0: mov eax,64h），对应 LiveTimelineKeyDataType.ToneCurve=100。
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.ToneCurve;
        public bool IsEnable;
        public float DepthMask;
        // +0x38 原生是 UnityEngine.AnimationCurve 引用（AlterUpdate_ToneCurve
        // 0x1adadad: mov rdx,[rbx+38h] 直接随 updateInfo 块一起按引用搬运）。
        public AnimationCurve ToneAnimationCurve;
        // Serialized ColorRGBA values, not curves (son1176_Camera TypeTree).
        public Color MinCorrectionLevel;
        public Color MaxCorrectionLevel;
        public AnimationCurve MaskToneCurve;
        public Color MaskMinCorrectionLevel;
        public Color MaskMaxCorrectionLevel;
    }

    [Serializable]
    public class LiveTimelineKeyToneCurveDataList :
        LiveTimelineKeyDataListTemplate<LiveTimelineKeyToneCurveData> { }
}
