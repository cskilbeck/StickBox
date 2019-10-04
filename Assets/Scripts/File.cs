using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class File
{
    public static Difficulty difficulty;

    public static Level load_level_by_id(int level_index)
    {
#if UNITY_EDITOR
        string name = $"level_{level_index,2:00}";
        Level l = AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}.asset");
        l.offset_board();
        return l;
#else
        string name = $"level_{level_index,2:00}";
        return Resources.Load<Level>(name);
#endif
    }

    public static Level load_level(int index)
    {
#if UNITY_EDITOR
        if (difficulty == null)
        {
            difficulty = AssetDatabase.LoadAssetAtPath<Difficulty>("Assets/Resources/sorted_levels.asset");
        }
        return load_level_by_id(difficulty.level_index[index]);
#else
        if(difficulty == null)
        {
            difficulty = Resources.Load<Difficulty>("sorted_levels");
        }
        return load_level_by_id(difficulty.level_index[index]);
#endif
    }
}
