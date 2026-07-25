using UnityEditor;
using UnityEngine;

public static class ConfigAssetCreator
{
    [MenuItem("🛠️Configs🛠️/Create Default Configs")]
    public static void CreateDefaultConfigs()
    {
        EnsureConfigsFolder();

        GameConfig gameConfig = LoadOrCreate<GameConfig>("0_Game Config.asset");
        EconomyConfig economyConfig = LoadOrCreate<EconomyConfig>("1_Economy.asset");
        UnitBalanceConfig unitBalanceConfig = LoadOrCreate<UnitBalanceConfig>("2_Unit Balance.asset");
        RaidWaveConfig raidWaveConfig = LoadOrCreate<RaidWaveConfig>("3_Raid Waves.asset");
        UiBalanceConfig uiBalanceConfig = LoadOrCreate<UiBalanceConfig>("4_UI Balance.asset");
        CameraConfig cameraConfig = LoadOrCreate<CameraConfig>("5_Camera.asset");
        OrcBirthConfig orcBirthConfig = LoadOrCreate<OrcBirthConfig>("6_Orc Birth.asset");
        GameplayConfig gameplayConfig = LoadOrCreate<GameplayConfig>("9_Gameplay Config.asset");

        SerializedObject serializedGameplayConfig = new SerializedObject(gameplayConfig);
        AssignIfEmpty(serializedGameplayConfig, "_gameConfig", gameConfig);
        AssignIfEmpty(serializedGameplayConfig, "_economy", economyConfig);
        AssignIfEmpty(serializedGameplayConfig, "_unitBalance", unitBalanceConfig);
        AssignIfEmpty(serializedGameplayConfig, "_raidWaves", raidWaveConfig);
        AssignIfEmpty(serializedGameplayConfig, "_uiBalance", uiBalanceConfig);
        AssignIfEmpty(serializedGameplayConfig, "_camera", cameraConfig);
        AssignIfEmpty(serializedGameplayConfig, "_orcBirth", orcBirthConfig);
        serializedGameplayConfig.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(gameplayConfig);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            Selection.activeObject = gameplayConfig;
            EditorGUIUtility.PingObject(gameplayConfig);
            ConfigsWindow.Open();
        }
    }

    private static void EnsureConfigsFolder()
    {
        if (!AssetDatabase.IsValidFolder(ConfigAssetPaths.ConfigsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "7_Configs");
        }
    }

    private static T LoadOrCreate<T>(string fileName) where T : ScriptableObject
    {
        string path = $"{ConfigAssetPaths.ConfigsFolder}/{fileName}";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void AssignIfEmpty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
        }
    }
}
