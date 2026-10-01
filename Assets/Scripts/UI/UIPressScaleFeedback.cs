using UnityEngine;
using UnityEngine.EventSystems;
/// <summary>
/// UI按压缩小反馈
/// </summary>

public class UIPressScaleFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;
    [SerializeField, Min(0.01f)] private float scaleSpeed = 18f;

    private Transform scaleTarget;
    private Vector3 normalScale;
    private bool isPressed;
    private int releaseAfterFrame = -1;

    public static void AddTo(GameObject target)
    {
        if (!target)
        {
            return;
        }

        UmaUIContainer container = target.GetComponent<UmaUIContainer>();
        GameObject eventTarget = container && container.Button ? container.Button.gameObject : target;

        UIPressScaleFeedback feedback = eventTarget.GetComponent<UIPressScaleFeedback>();
        if (!feedback)
        {
            feedback = eventTarget.AddComponent<UIPressScaleFeedback>();
        }

        feedback.SetScaleTarget(target.transform);
    }

    private void Awake()
    {
        SetScaleTarget(scaleTarget ? scaleTarget : transform);
    }

    private void OnEnable()
    {
        if (!scaleTarget)
        {
            scaleTarget = transform;
        }

        normalScale = scaleTarget.localScale;
        isPressed = false;
        releaseAfterFrame = -1;
    }

    private void OnDisable()
    {
        if (scaleTarget)
        {
            scaleTarget.localScale = normalScale;
        }

        isPressed = false;
        releaseAfterFrame = -1;
    }

    private void Update()
    {
        if (!scaleTarget)
        {
            return;
        }

        if (releaseAfterFrame >= 0 && Time.frameCount > releaseAfterFrame)
        {
            isPressed = false;
            releaseAfterFrame = -1;
        }

        Vector3 targetScale = normalScale * (isPressed ? pressedScale : 1f);
        scaleTarget.localScale = Vector3.Lerp( scaleTarget.localScale, targetScale, 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        releaseAfterFrame = -1;
        scaleTarget.localScale = normalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        releaseAfterFrame = Time.frameCount + 1;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        releaseAfterFrame = Time.frameCount + 1;
    }

    private void SetScaleTarget(Transform target)
    {
        scaleTarget = target;
        CenterPivotWithoutMoving(scaleTarget as RectTransform);
        normalScale = scaleTarget.localScale;
    }

    private static void CenterPivotWithoutMoving(RectTransform rectTransform)
    {
        if (!rectTransform || rectTransform.pivot == new Vector2(0.5f, 0.5f))
        {
            return;
        }

        Vector2 pivotDelta = new Vector2(0.5f, 0.5f) - rectTransform.pivot;
        Vector3 positionDelta = new Vector3( pivotDelta.x * rectTransform.rect.width * rectTransform.localScale.x, pivotDelta.y * rectTransform.rect.height * rectTransform.localScale.y, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localPosition += positionDelta;
    }
}
