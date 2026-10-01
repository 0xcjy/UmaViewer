using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class LiveBloomProbe : MonoBehaviour
{
    [Header("Bloom 测试参数")]
    [Range(0f, 10f)]
    public float bloomIntensity = 2.5f;

    [Range(0f, 5f)]
    public float bloomThreshold = 0.7f;

    [Range(0f, 1f)]
    public float bloomScatter = 0.75f;

    [Header("曝光测试")]
    [Range(-3f, 3f)]
    public float postExposure = 0.5f;

    private Volume _volume;
    private VolumeProfile _runtimeProfile;

    private Bloom _bloom;
    private ColorAdjustments _colorAdjustments;
    private Tonemapping _tonemapping;

    private void Awake()
    {
        EnablePostProcessingOnAllCameras();
        CreateRuntimeVolume();
        ApplyParameters();
    }

    private void LateUpdate()
    {
        // 你的 Live 会不断切换启用中的 Camera，
        // 所以测试阶段持续保证所有 Live Camera 都启用 HDR/Post Processing。
        EnablePostProcessingOnAllCameras();
        ApplyParameters();
    }

    private void EnablePostProcessingOnAllCameras()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera == null ||
                !camera.gameObject.scene.IsValid())
            {
                continue;
            }

            camera.allowHDR = true;

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<
                    UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
        }
    }

    private void CreateRuntimeVolume()
    {
        _volume = GetComponent<Volume>();

        if (_volume == null)
            _volume = gameObject.AddComponent<Volume>();

        _volume.isGlobal = true;
        _volume.priority = 1000f;
        _volume.weight = 1f;

        _runtimeProfile =
            ScriptableObject.CreateInstance<VolumeProfile>();

        _volume.sharedProfile = _runtimeProfile;

        _bloom = _runtimeProfile.Add<Bloom>(true);
        _colorAdjustments =
            _runtimeProfile.Add<ColorAdjustments>(true);
        _tonemapping =
            _runtimeProfile.Add<Tonemapping>(true);
    }

    private void ApplyParameters()
    {
        if (_bloom != null)
        {
            _bloom.active = true;
            _bloom.intensity.Override(bloomIntensity);
            _bloom.threshold.Override(bloomThreshold);
            _bloom.scatter.Override(bloomScatter);
            _bloom.clamp.Override(1000f);
            _bloom.highQualityFiltering.Override(true);
        }

        if (_colorAdjustments != null)
        {
            _colorAdjustments.active = true;
            _colorAdjustments.postExposure.Override(postExposure);
        }

        if (_tonemapping != null)
        {
            _tonemapping.active = true;
            _tonemapping.mode.Override(TonemappingMode.ACES);
        }
    }

    private void OnDestroy()
    {
        if (_runtimeProfile != null)
            Destroy(_runtimeProfile);
    }
}