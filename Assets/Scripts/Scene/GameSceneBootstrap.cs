using TMPro;
using UnityEngine;

[ExecuteAlways]
public sealed class GameSceneBootstrap : MonoBehaviour
{
    private const string _cameraName = "Main Camera";
    private const string _terrainName = "Terrain";
    private const string _cauldronName = "Cauldron";
    private const string _labelName = "Label";

    [SerializeField] private CameraConfig _cameraConfig = null;

    private static Sprite _whiteSprite;

    private void Awake()
    {
        EnsureSceneObjects();
    }

    private void OnEnable()
    {
        EnsureSceneObjects();
    }

    private void EnsureSceneObjects()
    {
        EnsureCamera();
        EnsureTerrain();
        EnsureCauldron();
    }

    private void EnsureCamera()
    {
        Transform cameraTransform = GetOrCreateChild(_cameraName);
        cameraTransform.localPosition = new Vector3(0f, 0f, -10f);
        cameraTransform.localRotation = Quaternion.identity;
        cameraTransform.localScale = Vector3.one;

        GameObject cameraObject = cameraTransform.gameObject;
        cameraObject.tag = "MainCamera";

        Camera camera = GetOrAddComponent<Camera>(cameraObject);
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.22f, 0.27f);

        GetOrAddComponent<AudioListener>(cameraObject);
        EdgeCameraPan cameraPan = GetOrAddComponent<EdgeCameraPan>(cameraObject);
        cameraPan.Configure(_cameraConfig);
        AddUniversalCameraDataIfAvailable(cameraObject);
    }

    private void EnsureTerrain()
    {
        Transform terrainTransform = GetOrCreateChild(_terrainName);
        terrainTransform.localPosition = new Vector3(0f, -3f, 0f);
        terrainTransform.localRotation = Quaternion.identity;
        terrainTransform.localScale = new Vector3(32f, 1f, 1f);

        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(terrainTransform.gameObject);
        renderer.sprite = GetWhiteSprite();
        renderer.color = new Color(0.72f, 0.76f, 0.68f);
        renderer.sortingOrder = 0;

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(terrainTransform.gameObject);
        collider.size = Vector2.one;
    }

    private void EnsureCauldron()
    {
        Transform cauldronTransform = GetOrCreateChild(_cauldronName);
        cauldronTransform.localPosition = new Vector3(0f, -1.65f, 0f);
        cauldronTransform.localRotation = Quaternion.identity;
        cauldronTransform.localScale = new Vector3(2.6f, 1.8f, 1f);

        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(cauldronTransform.gameObject);
        renderer.sprite = GetWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 5;

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(cauldronTransform.gameObject);
        collider.size = Vector2.one;

        EnsureCauldronLabel(cauldronTransform);
    }

    private void EnsureCauldronLabel(Transform cauldronTransform)
    {
        Transform labelTransform = GetOrCreateChild(cauldronTransform, _labelName);
        labelTransform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelTransform.localRotation = Quaternion.identity;
        labelTransform.localScale = new Vector3(0.4f, 0.55f, 1f);

        TextMeshPro label = GetOrAddComponent<TextMeshPro>(labelTransform.gameObject);
        label.text = "КОТЕЛ";
        label.fontSize = 4f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 10;
        label.rectTransform.sizeDelta = new Vector2(5f, 2f);
    }

    private Transform GetOrCreateChild(string childName)
    {
        return GetOrCreateChild(transform, childName);
    }

    private Transform GetOrCreateChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

        return gameObject.AddComponent<T>();
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return _whiteSprite;
    }

    private static void AddUniversalCameraDataIfAvailable(GameObject cameraObject)
    {
        System.Type cameraDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

        if (cameraDataType != null && cameraObject.GetComponent(cameraDataType) == null)
        {
            cameraObject.AddComponent(cameraDataType);
        }
    }
}
