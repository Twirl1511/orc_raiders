using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidEnemyRowView : MonoBehaviour
{
    private const float _rowWidth = 208f;

    private TextMeshProUGUI _nameText;
    private RectTransform _hpBarRoot;
    private Image _hpFill;
    private Image _attackFill;

    public static RaidEnemyRowView Create(RectTransform parent, int index)
    {
        GameObject rowObject = new GameObject($"Enemy Row {index + 1}", typeof(RectTransform), typeof(RaidEnemyRowView));
        rowObject.transform.SetParent(parent, false);

        RectTransform root = (RectTransform)rowObject.transform;
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(0f, -index * 52f);
        root.sizeDelta = new Vector2(_rowWidth, 46f);

        RaidEnemyRowView row = rowObject.GetComponent<RaidEnemyRowView>();
        row.Build(root);
        return row;
    }

    public void SetData(RaidEnemyViewData data)
    {
        _nameText.text = $"{data.Name}  {Mathf.CeilToInt(data.Hp)}/{Mathf.CeilToInt(data.MaxHp)} HP";
        SetBarFill(_hpFill, GetRatio(data.Hp, data.MaxHp));
        SetBarFill(_attackFill, data.AttackProgress);
    }

    private void Build(RectTransform root)
    {
        _nameText = CreateText(root, "Name", new Vector2(0f, 0f), new Vector2(_rowWidth, 20f), 13f);
        _hpFill = CreateBar(root, "HP", new Vector2(0f, -24f), new Vector2(_rowWidth, 14f), new Color(0.17f, 0.2f, 0.22f, 1f), new Color(0.9f, 0.25f, 0.25f, 1f), out _hpBarRoot);
        _attackFill = CreateBar(root, "Attack", new Vector2(0f, -42f), new Vector2(_rowWidth, 6f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f));
    }

    public System.Collections.IEnumerator ShakeHpBar()
    {
        Vector2 startPosition = _hpBarRoot.anchoredPosition;
        const float duration = 0.16f;
        const float amplitude = 5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 90f) * amplitude;
            _hpBarRoot.anchoredPosition = startPosition + new Vector2(offset, 0f);
            yield return null;
        }

        _hpBarRoot.anchoredPosition = startPosition;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)textObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Image CreateBar(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color fillColor)
    {
        return CreateBar(parent, name, anchoredPosition, size, backgroundColor, fillColor, out _);
    }

    private Image CreateBar(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color fillColor, out RectTransform barRoot)
    {
        GameObject backgroundObject = new GameObject($"{name} Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);
        RectTransform backgroundRect = SetupRect(backgroundObject, anchoredPosition, size);
        barRoot = backgroundRect;
        Image background = backgroundObject.GetComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        GameObject fillObject = new GameObject($"{name} Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(backgroundRect, false);
        RectTransform fillRect = SetupRect(fillObject, Vector2.zero, size);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        return fill;
    }

    private RectTransform SetupRect(GameObject gameObject, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rectTransform = (RectTransform)gameObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        return rectTransform;
    }

    private static void SetBarFill(Image fillImage, float value)
    {
        RectTransform rectTransform = fillImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static float GetRatio(float current, float max)
    {
        return max <= 0f ? 0f : Mathf.Clamp01(current / max);
    }
}
