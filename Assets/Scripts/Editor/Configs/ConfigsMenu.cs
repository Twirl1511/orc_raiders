using UnityEditor;

public static class ConfigsMenu
{
    [MenuItem("Tools/Configs/Open")]
    public static void OpenConfigsWindow()
    {
        ConfigsWindow.Open();
    }
}
