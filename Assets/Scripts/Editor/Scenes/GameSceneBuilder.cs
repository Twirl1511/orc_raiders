using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/GameScene.unity";
    private const string _prototypeArtFolder = "Assets/Art/Prototype";
    private const string _whiteSpritePath = "Assets/Art/Prototype/WhiteSquare.png";

    [MenuItem("Tools/Scenes/Create Game Scene")]
    public static void CreateGameScene()
    {
        Sprite whiteSprite = GetOrCreateWhiteSprite();
        CameraConfig cameraConfig = GetOrCreateCameraConfig();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(cameraConfig);
        CreateTerrain(whiteSprite);
        CreateCauldron(whiteSprite);

        EditorSceneManager.SaveScene(scene, _scenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created game scene: {_scenePath}");
    }

    private static void CreateCamera(CameraConfig cameraConfig)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.22f, 0.27f);

        cameraObject.AddComponent<AudioListener>();
        AddUniversalCameraDataIfAvailable(cameraObject);

        EdgeCameraPan edgeCameraPan = cameraObject.AddComponent<EdgeCameraPan>();
        edgeCameraPan.Configure(cameraConfig);
        SerializedObject serializedPan = new SerializedObject(edgeCameraPan);
        serializedPan.FindProperty("_config").objectReferenceValue = cameraConfig;
        serializedPan.FindProperty("_edgeSizePixels").floatValue = 28f;
        serializedPan.FindProperty("_panSpeed").floatValue = 8f;
        serializedPan.FindProperty("_minPosition").vector2Value = new Vector2(-12f, -4f);
        serializedPan.FindProperty("_maxPosition").vector2Value = new Vector2(12f, 4f);
        serializedPan.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateTerrain(Sprite whiteSprite)
    {
        GameObject terrain = new GameObject("Terrain");
        terrain.transform.position = new Vector3(0f, -3f, 0f);
        terrain.transform.localScale = new Vector3(32f, 1f, 1f);

        SpriteRenderer renderer = terrain.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = new Color(0.72f, 0.76f, 0.68f);
        renderer.sortingOrder = 0;

        BoxCollider2D collider = terrain.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private static void CreateCauldron(Sprite whiteSprite)
    {
        GameObject cauldron = new GameObject("Cauldron");
        cauldron.transform.position = new Vector3(0f, -1.65f, 0f);
        cauldron.transform.localScale = new Vector3(2.6f, 1.8f, 1f);

        SpriteRenderer renderer = cauldron.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;

        BoxCollider2D collider = cauldron.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(cauldron.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = new Vector3(0.4f, 0.55f, 1f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "КОТЕЛ";
        label.fontSize = 4f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 10;
        label.rectTransform.sizeDelta = new Vector2(5f, 2f);
    }

    private static Sprite GetOrCreateWhiteSprite()
    {
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Prototype");

        if (!File.Exists(_whiteSpritePath))
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(_whiteSpritePath, texture.EncodeToPNG());
        }

        AssetDatabase.ImportAsset(_whiteSpritePath);

        TextureImporter importer = AssetImporter.GetAtPath(_whiteSpritePath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(_whiteSpritePath);
    }

    private static CameraConfig GetOrCreateCameraConfig()
    {
        EnsureFolder("Assets", "7_Configs");

        CameraConfig cameraConfig = AssetDatabase.LoadAssetAtPath<CameraConfig>("Assets/7_Configs/5_Camera.asset");

        if (cameraConfig != null)
        {
            return cameraConfig;
        }

        cameraConfig = ScriptableObject.CreateInstance<CameraConfig>();
        AssetDatabase.CreateAsset(cameraConfig, "Assets/7_Configs/5_Camera.asset");
        AssetDatabase.SaveAssets();
        return cameraConfig;
    }

    private static void AddUniversalCameraDataIfAvailable(GameObject cameraObject)
    {
        System.Type cameraDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

        if (cameraDataType != null)
        {
            cameraObject.AddComponent(cameraDataType);
        }
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = $"{parent}/{folderName}";

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == _scenePath)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes;
                return;
            }
        }

        EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[scenes.Length + 1];

        for (int i = 0; i < scenes.Length; i++)
        {
            updatedScenes[i] = scenes[i];
        }

        updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(_scenePath, true);
        EditorBuildSettings.scenes = updatedScenes;
    }
}
