using UnityEditor;
using UnityEngine;
using Unity;

public static class Statics
{
    public static string level_name;

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
