using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class GameSceneBootstrap : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private CameraConfig _cameraConfig = null;
    [SerializeField] private OrcBirthConfig _orcBirthConfig = null;

    [Header("Scene References")]
    [SerializeField] private Camera _sceneCamera = null;
    [SerializeField] private EdgeCameraPan _cameraPan = null;
    [SerializeField] private OrcBirthSystem _orcBirthSystem = null;
    [SerializeField] private EventSystem _eventSystem = null;

    private void Awake()
    {
        ValidateSceneReferences();
        ConfigureExistingSystems();
    }

    private void ValidateSceneReferences()
    {
        LogMissingReference(_cameraConfig, nameof(_cameraConfig));
        LogMissingReference(_orcBirthConfig, nameof(_orcBirthConfig));
        LogMissingReference(_sceneCamera, nameof(_sceneCamera));
        LogMissingReference(_cameraPan, nameof(_cameraPan));
        LogMissingReference(_orcBirthSystem, nameof(_orcBirthSystem));
        LogMissingReference(_eventSystem, nameof(_eventSystem));

        if (_eventSystem != null && _eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            Debug.LogError($"{nameof(GameSceneBootstrap)} requires InputSystemUIInputModule on the assigned EventSystem.", this);
        }
    }

    private void ConfigureExistingSystems()
    {
        if (_cameraPan != null && _cameraConfig != null)
        {
            _cameraPan.Configure(_cameraConfig);
        }

        if (_orcBirthSystem != null && _orcBirthConfig != null && _sceneCamera != null)
        {
            _orcBirthSystem.Configure(_orcBirthConfig, _sceneCamera);
        }
    }

    private void LogMissingReference(UnityEngine.Object reference, string fieldName)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogError($"{nameof(GameSceneBootstrap)} missing required scene reference: {fieldName}. Assign it in the scene instead of creating it from code.", this);
    }
}
