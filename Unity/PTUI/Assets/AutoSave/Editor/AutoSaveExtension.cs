using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class AutoSaveExtension
{
    static AutoSaveExtension()
    {
        EditorApplication.playModeStateChanged += AutoSaveWhenPlaymodeStarts;
    }

    private static void AutoSaveWhenPlaymodeStarts(PlayModeStateChange state)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), null, false);
            AssetDatabase.SaveAssets();
        }
    }
}