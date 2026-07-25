using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class ConfigsWindow : EditorWindow
{
    private const float _toolbarButtonPadding = 24f;
    private const float _toolbarButtonMinWidth = 80f;
    private const float _toolbarHorizontalSpacing = 4f;

    private readonly List<Object> _configs = new List<Object>();
    private int _selectedIndex;
    private Editor _activeEditor;
    private Vector2 _scroll;

    public static void Open()
    {
        GetWindow<ConfigsWindow>("Configs");
    }

    private void OnEnable()
    {
        LoadConfigs();
    }

    private void OnDisable()
    {
        if (_activeEditor != null)
        {
            DestroyImmediate(_activeEditor);
        }
    }

    private void LoadConfigs()
    {
        _configs.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ConfigAssetPaths.ConfigsFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            if (asset != null)
            {
                _configs.Add(asset);
            }
        }

        _configs.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.Ordinal));
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _configs.Count - 1));
        _activeEditor = null;
    }

    private void OnGUI()
    {
        DrawTopBar();

        if (_configs.Count == 0)
        {
            EditorGUILayout.HelpBox($"No configs found in {ConfigAssetPaths.ConfigsFolder}.", MessageType.Warning);
            return;
        }

        DrawConfigsToolbar();
        DrawSelectedConfig();
    }

    private void DrawTopBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(64f)))
        {
            LoadConfigs();
        }

        if (GUILayout.Button("Create Defaults", EditorStyles.toolbarButton, GUILayout.Width(104f)))
        {
            ConfigAssetCreator.CreateDefaultConfigs();
            LoadConfigs();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawSelectedConfig()
    {
        Object current = _configs[_selectedIndex];

        if (_activeEditor == null || _activeEditor.target != current)
        {
            if (_activeEditor != null)
            {
                DestroyImmediate(_activeEditor);
            }

            _activeEditor = Editor.CreateEditor(current);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        _activeEditor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }

    private void DrawConfigsToolbar()
    {
        float availableWidth = Mathf.Max(_toolbarButtonMinWidth, position.width - _toolbarHorizontalSpacing);
        float rowWidth = 0f;

        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        for (int i = 0; i < _configs.Count; i++)
        {
            Object config = _configs[i];
            float buttonWidth = GetConfigButtonWidth(config, availableWidth);

            if (rowWidth > 0f && rowWidth + buttonWidth > availableWidth)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                rowWidth = 0f;
            }

            DrawConfigButton(i, config, buttonWidth);
            rowWidth += buttonWidth + _toolbarHorizontalSpacing;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private float GetConfigButtonWidth(Object config, float availableWidth)
    {
        GUIContent content = new GUIContent(config.name);
        float contentWidth = EditorStyles.toolbarButton.CalcSize(content).x + _toolbarButtonPadding;
        float width = Mathf.Max(_toolbarButtonMinWidth, contentWidth);

        return Mathf.Min(width, availableWidth);
    }

    private void DrawConfigButton(int index, Object config, float buttonWidth)
    {
        GUIContent content = new GUIContent(config.name, AssetDatabase.GetAssetPath(config));
        bool selected = index == _selectedIndex;

        if (GUILayout.Toggle(selected, content, EditorStyles.toolbarButton, GUILayout.Width(buttonWidth)) && !selected)
        {
            _selectedIndex = index;
            _activeEditor = null;
        }
    }
}
