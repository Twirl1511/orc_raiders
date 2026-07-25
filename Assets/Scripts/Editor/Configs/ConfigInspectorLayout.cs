using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ConfigInspectorLayout
{
    private const string _labelWidthKey = "LabelWidth";
    private const string _fieldWidthKey = "FieldWidth";
    private const float _defaultLabelWidth = 142.3f;
    private const float _defaultFieldWidth = 165.41f;
    private const float _minLabelWidth = 80f;
    private const float _minFieldWidth = 40f;
    private const float _rangeFieldMinWidth = 260f;

    public static bool CanDrawTarget(UnityEngine.Object target)
    {
        string assetPath = AssetDatabase.GetAssetPath(target);
        return assetPath.StartsWith($"{ConfigAssetPaths.ConfigsFolder}/", StringComparison.Ordinal);
    }

    public static void DrawSerializedObject(SerializedObject serializedObject)
    {
        serializedObject.Update();

        string assetKey = GetAssetKey(serializedObject.targetObject);
        DrawLayoutSettings(assetKey);

        float labelWidth = GetWidth(assetKey, _labelWidthKey, _defaultLabelWidth);
        float fieldWidth = GetWidth(assetKey, _fieldWidthKey, _defaultFieldWidth);
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        float previousFieldWidth = EditorGUIUtility.fieldWidth;

        try
        {
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUIUtility.fieldWidth = fieldWidth;
            DrawProperties(serializedObject, labelWidth, fieldWidth);
            serializedObject.ApplyModifiedProperties();
        }
        finally
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUIUtility.fieldWidth = previousFieldWidth;
        }
    }

    private static string GetAssetKey(UnityEngine.Object target)
    {
        string assetPath = AssetDatabase.GetAssetPath(target);
        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

        if (string.IsNullOrEmpty(assetGuid))
        {
            return target.GetType().FullName;
        }

        return assetGuid;
    }

    private static void DrawLayoutSettings(string assetKey)
    {
        float labelWidth = GetWidth(assetKey, _labelWidthKey, _defaultLabelWidth);
        float fieldWidth = GetWidth(assetKey, _fieldWidthKey, _defaultFieldWidth);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Inspector Layout", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        labelWidth = EditorGUILayout.FloatField("Label Width", labelWidth);
        fieldWidth = EditorGUILayout.FloatField("Field Width", fieldWidth);

        if (EditorGUI.EndChangeCheck())
        {
            SetWidth(assetKey, _labelWidthKey, Mathf.Max(_minLabelWidth, labelWidth));
            SetWidth(assetKey, _fieldWidthKey, Mathf.Max(_minFieldWidth, fieldWidth));
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private static float GetWidth(string assetKey, string widthKey, float defaultWidth)
    {
        return EditorPrefs.GetFloat(GetEditorPrefsKey(assetKey, widthKey), defaultWidth);
    }

    private static void SetWidth(string assetKey, string widthKey, float width)
    {
        EditorPrefs.SetFloat(GetEditorPrefsKey(assetKey, widthKey), width);
    }

    private static string GetEditorPrefsKey(string assetKey, string widthKey)
    {
        return $"{nameof(ConfigInspectorLayout)}.{assetKey}.{widthKey}";
    }

    private static void DrawProperties(SerializedObject serializedObject, float labelWidth, float fieldWidth)
    {
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            DrawProperty(property, labelWidth, fieldWidth);
            enterChildren = false;
        }
    }

    private static void DrawProperty(SerializedProperty property, float labelWidth, float fieldWidth)
    {
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = EditorGUILayout.GetControlRect(true, propertyHeight);
        float propertyFieldWidth = GetPropertyFieldWidth(property, fieldWidth);
        float contentWidth = labelWidth + propertyFieldWidth;
        rect.width = Mathf.Min(rect.width, contentWidth);

        using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
        {
            EditorGUI.PropertyField(rect, property, true);
        }
    }

    private static float GetPropertyFieldWidth(SerializedProperty property, float fieldWidth)
    {
        if (!HasRangeAttribute(property))
        {
            return fieldWidth;
        }

        return Mathf.Max(fieldWidth, _rangeFieldMinWidth);
    }

    private static bool HasRangeAttribute(SerializedProperty property)
    {
        FieldInfo fieldInfo = GetFieldInfo(property);

        if (fieldInfo == null)
        {
            return false;
        }

        return fieldInfo.GetCustomAttributes(typeof(RangeAttribute), true).Length > 0;
    }

    private static FieldInfo GetFieldInfo(SerializedProperty property)
    {
        Type currentType = property.serializedObject.targetObject.GetType();
        FieldInfo fieldInfo = null;
        string propertyPath = property.propertyPath.Replace(".Array.data[", "[");
        string[] pathParts = propertyPath.Split('.');

        for (int i = 0; i < pathParts.Length; i++)
        {
            string fieldName = GetFieldName(pathParts[i]);
            fieldInfo = GetFieldInfo(currentType, fieldName);

            if (fieldInfo == null)
            {
                return null;
            }

            currentType = GetNextFieldType(fieldInfo.FieldType, pathParts[i]);
        }

        return fieldInfo;
    }

    private static string GetFieldName(string pathPart)
    {
        int collectionIndex = pathPart.IndexOf("[", StringComparison.Ordinal);

        if (collectionIndex < 0)
        {
            return pathPart;
        }

        return pathPart.Substring(0, collectionIndex);
    }

    private static FieldInfo GetFieldInfo(Type type, string fieldName)
    {
        BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        while (type != null)
        {
            FieldInfo fieldInfo = type.GetField(fieldName, bindingFlags);

            if (fieldInfo != null)
            {
                return fieldInfo;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static Type GetNextFieldType(Type fieldType, string pathPart)
    {
        if (!pathPart.Contains("["))
        {
            return fieldType;
        }

        if (fieldType.IsArray)
        {
            return fieldType.GetElementType();
        }

        if (fieldType.IsGenericType)
        {
            return fieldType.GetGenericArguments()[0];
        }

        return fieldType;
    }
}
