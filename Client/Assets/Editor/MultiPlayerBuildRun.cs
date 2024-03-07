using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MultiPlayerBuildRun
{
    [MenuItem("Tools/Run MultiPlayer/2 Player")]
    static void PreformWin64Build2()
    {
        PreformWin64Build(2);
    }
    [MenuItem("Tools/Run MultiPlayer/3 Player")]
    static void PreformWin64Build3()
    {
        PreformWin64Build(3);
    }
    [MenuItem("Tools/Run MultiPlayer/4 Player")]
    static void PreformWin64Build4()
    {
        PreformWin64Build(4);
    }

    static void PreformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);

        for(int i = 1; i <= playerCount; i++)
        {
            BuildPipeline.BuildPlayer(GetScenePaths(),
                "Builds/Win64/" + GetProjectName() + i.ToString() + "/" + GetProjectName() + i.ToString() + ".exe",
                BuildTarget.StandaloneWindows, BuildOptions.AutoRunPlayer);
        }
    }

    static string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        return s[s.Length - 2];
    }

    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];

        for(int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        return scenes;
    }
}
