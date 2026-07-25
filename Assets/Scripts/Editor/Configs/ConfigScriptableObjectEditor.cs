using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScriptableObject), true)]
public sealed class ConfigScriptableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!ConfigInspectorLayout.CanDrawTarget(target))
        {
            DrawDefaultInspector();
            return;
        }

        ConfigInspectorLayout.DrawSerializedObject(serializedObject);
    }
}
