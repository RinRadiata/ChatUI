using TMPro;
using UnityEngine;

public class MarqueeTextUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform textRect;
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private TMP_Text targetText;

    [Header("Movement")]
    [SerializeField] private float speed = 120f;
    [SerializeField] private bool leftToRight = false;
    [SerializeField] private bool resetOnEnable = true;

    private void Reset()
    {
        textRect = GetComponent<RectTransform>();
        targetText = GetComponent<TMP_Text>();

        if (transform.parent != null)
            maskRect = transform.parent.GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (textRect == null)
            textRect = GetComponent<RectTransform>();

        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (maskRect == null && transform.parent != null)
            maskRect = transform.parent.GetComponent<RectTransform>();

        if (targetText != null)
        {
            targetText.textWrappingMode = TextWrappingModes.NoWrap;
            targetText.overflowMode = TextOverflowModes.Overflow;
            targetText.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        if (resetOnEnable)
            ResetPosition();
    }

    private void Update()
    {
        if (textRect == null || maskRect == null)
            return;

        float maskWidth = maskRect.rect.width;
        float textWidth = GetTextWidth();

        Vector2 pos = textRect.anchoredPosition;

        if (leftToRight)
        {
            pos.x += speed * Time.deltaTime;

            if (pos.x > maskWidth)
                pos.x = -textWidth;
        }
        else
        {
            pos.x -= speed * Time.deltaTime;

            if (pos.x < -textWidth)
                pos.x = maskWidth;
        }

        textRect.anchoredPosition = pos;
    }

    public void ResetPosition()
    {
        if (textRect == null || maskRect == null)
            return;

        float maskWidth = maskRect.rect.width;
        float textWidth = GetTextWidth();

        Vector2 pos = textRect.anchoredPosition;
        pos.y = 0f;

        if (leftToRight)
            pos.x = -textWidth;
        else
            pos.x = maskWidth;

        textRect.anchoredPosition = pos;
    }

    private float GetTextWidth()
    {
        if (targetText != null)
        {
            targetText.ForceMeshUpdate();
            return Mathf.Max(targetText.preferredWidth + 40f, 1f);
        }

        return Mathf.Max(textRect.rect.width, 1f);
    }
}