// MainMenuController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Optional: default scene to load")]
    [SerializeField] private string defaultSceneName = "SampleScene";

    /// <summary>
    /// Загружает сцену по имени (привяжите к кнопке и передайте параметр в OnClick).
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(defaultSceneName);
        }
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Загружает сцену из поля defaultSceneName.
    /// Удобно, если хотите не передавать параметр в OnClick.
    /// </summary>
    public void LoadDefaultScene()
    {
        if (string.IsNullOrEmpty(defaultSceneName))
        {
            Debug.LogWarning("[MainMenuController] defaultSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(defaultSceneName);
    }

    /// <summary>
    /// Выход из игры (в редакторе останавливает Play Mode).
    /// </summary>
    public void ExitGame()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
}
