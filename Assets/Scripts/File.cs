using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class File
{
    public static Difficulty difficulty;

    public static Level load_level(int index)
    {
#if UNITY_EDITOR
        if (difficulty == null)
        {
            difficulty = AssetDatabase.LoadAssetAtPath<Difficulty>("Assets/Resources/sorted_levels.asset");
        }
        int level_index = difficulty.level_index[index];
        string name = $"level_{level_index,2:00}";
        return AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}.asset");
#else
        if(difficulty == null)
        {
            difficulty = Resources.Load<Difficulty>("sorted_levels");
        }
        int level_index = difficulty.level_index[index];
        string name = $"level_{level_index,2:00}";
        return Resources.Load<Level>(name);
#endif
    }
}
