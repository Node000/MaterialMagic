using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Removes the authored QA subtree from player scenes unless QA is explicitly enabled.</summary>
internal sealed class DeveloperUnlockBuildStripper : IProcessSceneWithReport
{
    private const string QaDefine = "MATERIALMAGIC_QA_UNLOCK";

    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Unity also processes scenes on entering Play Mode, without a player build report.
        if (report == null)
            return;

        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(report.summary.platformGroup);
        StripForBuild(scene, report.summary.options, defines);
    }

    internal static void StripForBuild(Scene scene, BuildOptions options, string defines)
    {
        if ((options & BuildOptions.Development) != 0 &&
            Array.Exists(defines.Split(';'), define => define.Trim() == QaDefine))
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (DeveloperUnlockPanelUI panel in root.GetComponentsInChildren<DeveloperUnlockPanelUI>(true))
            {
                if (panel != null)
                    UnityEngine.Object.DestroyImmediate(panel.gameObject);
            }
        }
    }
}
