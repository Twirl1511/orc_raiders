using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public readonly struct OrcBirthUiReferences
{
    public readonly Canvas Canvas;
    public readonly RectTransform AvailableDiceRoot;
    public readonly RectTransform SelectedDiceRoot;
    public readonly Button DiceButtonTemplate;
    public readonly TextMeshProUGUI SelectedDiceLabel;
    public readonly TextMeshProUGUI StatusText;
    public readonly TextMeshProUGUI OrcInfoText;
    public readonly Button CreateOrcButton;

    public OrcBirthUiReferences(
        Canvas canvas,
        RectTransform availableDiceRoot,
        RectTransform selectedDiceRoot,
        Button diceButtonTemplate,
        TextMeshProUGUI selectedDiceLabel,
        TextMeshProUGUI statusText,
        TextMeshProUGUI orcInfoText,
        Button createOrcButton)
    {
        Canvas = canvas;
        AvailableDiceRoot = availableDiceRoot;
        SelectedDiceRoot = selectedDiceRoot;
        DiceButtonTemplate = diceButtonTemplate;
        SelectedDiceLabel = selectedDiceLabel;
        StatusText = statusText;
        OrcInfoText = orcInfoText;
        CreateOrcButton = createOrcButton;
    }
}

public static class OrcBirthUiBuilder
{
    private const string _canvasName = "Orc Birth UI";

    public static OrcBirthUiReferences Ensure(Transform parent, int requiredDiceCount, out bool changed)
    {
        changed = false;

        RectTransform canvasRect = GetOrCreateChild<RectTransform>(parent, _canvasName, out bool canvasCreated);
        changed |= canvasCreated;

        Canvas canvas = GetOrAddComponent<Canvas>(canvasRect.gameObject, ref changed);
        GraphicRaycaster raycaster = GetOrAddComponent<GraphicRaycaster>(canvasRect.gameObject, ref changed);
        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasRect.gameObject, ref changed);

        if (canvasCreated)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        _ = raycaster;

        RectTransform birthPanel = EnsurePanel(
            canvasRect,
            "Orc Birth Panel",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(20f, 20f),
            new Vector2(560f, 360f),
            ref changed);

        RectTransform infoPanel = EnsurePanel(
            canvasRect,
            "Orc Info Panel",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -20f),
            new Vector2(320f, 220f),
            ref changed);

        EnsureText(birthPanel, "Title", "Рождение орков", 28f, TextAlignmentOptions.Center, 34f, ref changed);
        EnsureText(birthPanel, "Available Dice Label", "Доступные кубики", 20f, TextAlignmentOptions.Left, 26f, ref changed);
        RectTransform availableDiceRoot = EnsureGrid(birthPanel, "Available Dice Grid", 96f, new Vector2(86f, 34f), ref changed);
        Button diceButtonTemplate = EnsureButton(availableDiceRoot, "Dice Button Template", "Кубик", 34f, ref changed);
        TextMeshProUGUI selectedDiceLabel = EnsureText(birthPanel, "Selected Dice Label", $"Кубики в котле: 0/{requiredDiceCount}", 20f, TextAlignmentOptions.Left, 26f, ref changed);
        RectTransform selectedDiceRoot = EnsureGrid(birthPanel, "Selected Dice Grid", 66f, new Vector2(86f, 34f), ref changed);
        Button createOrcButton = EnsureButton(birthPanel, "Create Orc Button", "Создать орка", 42f, ref changed);
        TextMeshProUGUI statusText = EnsureText(birthPanel, "Status", $"Выбери минимум {requiredDiceCount} кубиков и нажми кнопку.", 18f, TextAlignmentOptions.Left, 72f, ref changed);

        EnsureText(infoPanel, "Orc Info Title", "Орк", 26f, TextAlignmentOptions.Center, 34f, ref changed);
        TextMeshProUGUI orcInfoText = EnsureText(infoPanel, "Orc Info Text", "Созданные орки появятся рядом с котлом.\nКлик по орку покажет статы.", 18f, TextAlignmentOptions.Left, 150f, ref changed);

        return new OrcBirthUiReferences(canvas, availableDiceRoot, selectedDiceRoot, diceButtonTemplate, selectedDiceLabel, statusText, orcInfoText, createOrcButton);
    }

    public static void ValidateEventSystem(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            Debug.LogError("GameScene requires an EventSystem assigned in the scene. Add one manually and wire it to GameSceneBootstrap.");
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            Debug.LogError("GameScene EventSystem requires InputSystemUIInputModule. Add it manually on the EventSystem object.");
        }
    }

    private static RectTransform EnsurePanel(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        ref bool changed)
    {
        RectTransform rectTransform = GetOrCreateChild<RectTransform>(parent, objectName, out bool created);
        changed |= created;

        Image image = GetOrAddComponent<Image>(rectTransform.gameObject, ref changed);
        VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(rectTransform.gameObject, ref changed);

        if (created)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            image.color = new Color(0.08f, 0.09f, 0.1f, 0.86f);

            layoutGroup.padding = new RectOffset(12, 12, 12, 12);
            layoutGroup.spacing = 6f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
        }

        return rectTransform;
    }

    private static TextMeshProUGUI EnsureText(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        float preferredHeight,
        ref bool changed)
    {
        RectTransform rectTransform = GetOrCreateChild<RectTransform>(parent, objectName, out bool created);
        changed |= created;

        TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(rectTransform.gameObject, ref changed);
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(rectTransform.gameObject, ref changed);

        if (created)
        {
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            layoutElement.preferredHeight = preferredHeight;
        }

        return label;
    }

    private static RectTransform EnsureGrid(Transform parent, string objectName, float preferredHeight, Vector2 cellSize, ref bool changed)
    {
        RectTransform rectTransform = GetOrCreateChild<RectTransform>(parent, objectName, out bool created);
        changed |= created;

        GridLayoutGroup grid = GetOrAddComponent<GridLayoutGroup>(rectTransform.gameObject, ref changed);
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(rectTransform.gameObject, ref changed);

        if (created)
        {
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            layoutElement.preferredHeight = preferredHeight;
        }

        return rectTransform;
    }

    private static Button EnsureButton(Transform parent, string objectName, string text, float preferredHeight, ref bool changed)
    {
        RectTransform rectTransform = GetOrCreateChild<RectTransform>(parent, objectName, out bool created);
        changed |= created;

        Image image = GetOrAddComponent<Image>(rectTransform.gameObject, ref changed);
        Button button = GetOrAddComponent<Button>(rectTransform.gameObject, ref changed);
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(rectTransform.gameObject, ref changed);
        TextMeshProUGUI label = EnsureButtonLabel(rectTransform, text, ref changed);

        if (created)
        {
            image.color = Color.white;
            layoutElement.preferredHeight = preferredHeight;
            label.color = Color.black;
        }

        return button;
    }

    private static TextMeshProUGUI EnsureButtonLabel(Transform parent, string text, ref bool changed)
    {
        RectTransform rectTransform = GetOrCreateChild<RectTransform>(parent, "Label", out bool created);
        changed |= created;

        TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(rectTransform.gameObject, ref changed);

        if (created)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            label.text = text;
            label.fontSize = 18f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        return label;
    }

    private static T GetOrCreateChild<T>(Transform parent, string childName, out bool created) where T : Component
    {
        Transform existingChild = parent.Find(childName);

        if (existingChild != null)
        {
            created = false;
            T existingComponent = existingChild.GetComponent<T>();

            if (existingComponent != null)
            {
                return existingComponent;
            }

            return existingChild.gameObject.AddComponent<T>();
        }

        GameObject childObject = new GameObject(childName, typeof(T));
        childObject.transform.SetParent(parent, false);
        created = true;
        return childObject.GetComponent<T>();
    }

    private static T GetOrAddComponent<T>(GameObject gameObject, ref bool changed) where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

        changed = true;
        return gameObject.AddComponent<T>();
    }

}
