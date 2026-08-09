using UnityEngine;

/// <summary>
/// Keeps a DynamicBone capsule aligned with the animated hip-to-knee segment.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class SkirtLegCollisionProxy : MonoBehaviour
{
    public Transform StartBone;
    public Transform EndBone;
    public DynamicBoneCollider Collider;
    public float RadiusScale;

    private void Update()
    {
        UpdateColliderTransform();
    }

    private void LateUpdate()
    {
        // Refresh after the character animation has written the current leg pose,
        // before DynamicBone performs its late-frame collision solve.
        UpdateColliderTransform();
    }

    public void UpdateColliderTransform()
    {
        if (StartBone == null || EndBone == null || Collider == null)
            return;

        Vector3 segment = EndBone.position - StartBone.position;
        float length = segment.magnitude;
        if (length <= Mathf.Epsilon)
            return;

        transform.SetPositionAndRotation(
            StartBone.position + segment * 0.5f,
            Quaternion.LookRotation(segment));

        if (RadiusScale > 0f)
            Collider.m_Radius = length * RadiusScale;

        // DynamicBone measures capsule height including both rounded ends.
        Collider.m_Height = length + Collider.m_Radius * 2f;
    }
}
