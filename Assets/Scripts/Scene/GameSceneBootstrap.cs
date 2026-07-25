using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteAlways]
public sealed class GameSceneBootstrap : MonoBehaviour
{
    private const string _cameraName = "Main Camera";
    private const string _terrainName = "Terrain";
    private const string _cauldronName = "Cauldron";
    private const string _spriteName = "Sprite";
    private const string _colliderName = "Collider";
    private const string _labelName = "Label";
    private static readonly Vector2 _cauldronSize = new Vector2(2.6f, 1.8f);

    [SerializeField] private CameraConfig _cameraConfig = null;
    [SerializeField] private OrcBirthConfig _orcBirthConfig = null;
    [SerializeField] private EventSystem _eventSystem = null;

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
        Camera sceneCamera = EnsureCamera();
        EnsureTerrain();
        EnsureCauldron();
        OrcBirthUiReferences uiReferences = EnsureOrcBirthUi();
        EnsureEventSystem();
        EnsureOrcBirthSystem(sceneCamera, uiReferences);
    }

    private Camera EnsureCamera()
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

        return camera;
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
        cauldronTransform.localScale = Vector3.one;

        bool removedOldComponents = false;
        removedOldComponents |= RemoveComponentIfExists<SpriteRenderer>(cauldronTransform.gameObject);
        removedOldComponents |= RemoveComponentIfExists<BoxCollider2D>(cauldronTransform.gameObject);

        EnsureCauldronSprite(cauldronTransform);
        EnsureCauldronCollider(cauldronTransform);
        EnsureCauldronLabel(cauldronTransform);

        if (removedOldComponents)
        {
            MarkSceneDirty();
        }
    }

    private void EnsureCauldronSprite(Transform cauldronTransform)
    {
        Transform spriteTransform = GetOrCreateChild(cauldronTransform, _spriteName);
        spriteTransform.localPosition = Vector3.zero;
        spriteTransform.localRotation = Quaternion.identity;
        spriteTransform.localScale = Vector3.one;

        RemoveComponentIfExists<BoxCollider2D>(spriteTransform.gameObject);

        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(spriteTransform.gameObject);
        renderer.sprite = GetWhiteSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = _cauldronSize;
    }

    private void EnsureCauldronCollider(Transform cauldronTransform)
    {
        Transform colliderTransform = GetOrCreateChild(cauldronTransform, _colliderName);
        colliderTransform.localPosition = Vector3.zero;
        colliderTransform.localRotation = Quaternion.identity;
        colliderTransform.localScale = Vector3.one;

        RemoveComponentIfExists<SpriteRenderer>(colliderTransform.gameObject);

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(colliderTransform.gameObject);
        collider.size = _cauldronSize;
        collider.offset = Vector2.zero;
    }

    private void EnsureCauldronLabel(Transform cauldronTransform)
    {
        Transform labelTransform = GetOrCreateChild(cauldronTransform, _labelName);
        labelTransform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelTransform.localRotation = Quaternion.identity;
        labelTransform.localScale = Vector3.one;

        TextMeshPro label = GetOrAddComponent<TextMeshPro>(labelTransform.gameObject);
        label.text = "КОТЕЛ";
        label.fontSize = 4f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 10;
        label.rectTransform.sizeDelta = new Vector2(2.4f, 1.4f);
    }

    private OrcBirthUiReferences EnsureOrcBirthUi()
    {
        int requiredDiceCount = _orcBirthConfig != null ? _orcBirthConfig.RequiredDiceCount : 6;
        OrcBirthUiReferences uiReferences = OrcBirthUiBuilder.Ensure(transform, requiredDiceCount, out bool changed);

        if (changed)
        {
            MarkSceneDirty();
        }

        return uiReferences;
    }

    private void EnsureEventSystem()
    {
        OrcBirthUiBuilder.ValidateEventSystem(_eventSystem);
    }

    private void EnsureOrcBirthSystem(Camera sceneCamera, OrcBirthUiReferences uiReferences)
    {
        bool changed = gameObject.GetComponent<OrcBirthSystem>() == null;
        OrcBirthSystem orcBirthSystem = GetOrAddComponent<OrcBirthSystem>(gameObject);
        changed |= orcBirthSystem.Configure(_orcBirthConfig, sceneCamera);
        changed |= orcBirthSystem.ConfigureUi(uiReferences);

        if (changed)
        {
            MarkSceneDirty();
        }
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

    private static bool RemoveComponentIfExists<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
        {
            return false;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }

        return true;
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

        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
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

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
