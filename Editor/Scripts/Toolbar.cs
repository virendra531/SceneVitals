#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityToolbarExtender; //Add in package manager https://github.com/marijnz/unity-toolbar-extender.git
using System.Collections.Generic;
using System.Linq;

namespace SceneVitals
{
    [InitializeOnLoad]
    public static class Toolbar
    {
        static Toolbar()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);
        }

        static void OnToolbarGUI()
        {
            GUILayout.FlexibleSpace();

            GUI.color = Color.white * 0.75f;
            GUI.contentColor = Color.white * 1.15f;

            string cannotTestReason = GetTestButtonErrorString();
            string buttonText, buttonTooltipText;
            using (new EditorGUI.DisabledScope(disabled: !string.IsNullOrEmpty(cannotTestReason)))
            {

                Scene scene = EditorSceneManager.GetActiveScene();
                // buttonText = "▶️ Test Active Scene";
                buttonText = "Test Active Scene";
                buttonTooltipText = $"Check console window for report";

                if (GUILayout.Button(new GUIContent($"{buttonText}", EditorGUIUtility.IconContent("d_DebuggerEnabled").image, buttonTooltipText), GUILayout.ExpandWidth(false)))
                {
                    ScenePerformance.TestScenePerformance(EditorSceneManager.GetActiveScene());
                }

            }

            // -------------------- Textures --------------------
            buttonTooltipText = $"Select texture(selected 40) that has high memory usage";
            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("d_PreTextureMipMapHigh").image, buttonTooltipText), GUILayout.ExpandWidth(false)))
            {
                PerformanceResponse response = ScenePerformance.GetActiveScenePerformanceResponse();
                Scene scene = EditorSceneManager.GetActiveScene();
                // if (response.sharedTexturePercent > 1f)
                // {
                    UnityEngine.Debug.Log(
                        $"Scene {scene.name} has <color=yellow>too many shared textures.</color>\n"
                        + $"For WebGL it is encouraged to be be limited to {PerformanceResponse.MAX_SUGGESTED_SHARED_TEXTURE_MB} MB of shared textures. \n"
                        + "High memory usage can cause application crashes on lower end devices. It is highly recommended that you stay within the suggested limits. \n"
                        + "Compressing your textures will help reduce their size.\n"
                        + "Here's a list of all textures(40 selected) used by the scene:\n - " + "<color=yellow>" + string.Join("\n - ", response.textureMemorySizesMB.Take(40).Select(m => $"<color=red>{m.Item2:0.00}MB</color> - {m.Item1}")) + "</color>\n"
                    );

                    // SelectTextureFiles(response.textureMemorySizesMB.Take(40).ToList());
                    // SelectedFilesWindow.ShowWindow(SelectFiles(response.textureMemorySizesMB.Take(40).ToList()));
                    Selection.objects = SelectFiles(response.textureMemorySizesMB.Take(40).ToList());
                    SelectedFilesWindow.ShowWindow();
                // }
            }
            // -------------------- Textures --------------------

            // -------------------- Meshes --------------------
            buttonTooltipText = $"Select mesh(selected 30) that has high memory usage";
            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("d_PreMatCube").image, buttonTooltipText), GUILayout.ExpandWidth(false)))
            {
                PerformanceResponse response = ScenePerformance.GetActiveScenePerformanceResponse();
                Scene scene = EditorSceneManager.GetActiveScene();
                // if (response.vertPercent > 1f)
                // {
                    UnityEngine.Debug.Log(
                        $"Scene {scene.name} has <color=yellow>too many vertices.</color>\n"
                        + "The scene has too many high detail models. It is recommended that you stay within the suggested limits or your asset may not perform well on all platforms.\n"
                        + "Here's a list of all objects(30 selected) with high vertex counts:\n - " + "<color=yellow>" + string.Join("\n - ", response.meshVertCounts.Take(30).Select(m => $"<color=red>{m.Item2}</color> - {m.Item1}")) + "</color>\n"
                    );

                    // SelectedFilesWindow.ShowWindow(SelectFiles(response.meshVertCounts.Take(30).ToList()));
                    Selection.objects = SelectFiles(response.meshVertCounts.Take(30).ToList());
                    SelectedFilesWindow.ShowWindow();
                // }
            }
            // -------------------- Meshes --------------------

            // -------------------- Mesh Colliders --------------------
            buttonTooltipText = $"Select mesh collider(selected 30) that high density mesh colliders";
            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("ObjectMode").image, buttonTooltipText), GUILayout.ExpandWidth(false)))
            {
                PerformanceResponse response = ScenePerformance.GetActiveScenePerformanceResponse();
                Scene scene = EditorSceneManager.GetActiveScene();
                // if (response.meshColliderVertPercent > 1f)
                // {
                    UnityEngine.Debug.Log(
                        $"Scene {scene.name} has a <color=yellow>lot of high density mesh colliders ({response.meshColliderVerts}/{PerformanceResponse.MAX_SUGGESTED_COLLIDER_VERTS}).</color>\n"
                        + "You should try to use primitives or low density meshes for colliders where possible. \n"
                        + "High density collision geometry will impact the performance of your space.\n"
                        + "Here's a list of all objects(30 selected) with high density mesh colliders:\n - " + "<color=yellow>" + string.Join("\n - ", response.meshColliderVertCounts.Take(30).Select(m => $"<color=red>{m.Item2}</color> - {m.Item1}")) + "</color>\n"
                    );

                    // SelectedFilesWindow.ShowWindow(SelectFiles(response.meshColliderVertCounts.Take(30).ToList()));
                    HideAllOtherGameObjectInHierarchy(SelectFiles(response.meshColliderVertCounts.Take(30).ToList()));
                // }
            }
            // -------------------- Mesh Colliders --------------------

            GUI.color = Color.white;
            GUILayout.Space(15);
        }

        private static string GetTestButtonErrorString()
        {
            // if (EditorApplication.isPlayingOrWillChangePlaymode)
            //     return "Feature disabled while in play mode";

            return null;
        }
        public static UnityEngine.Object[] SelectFiles<T>(List<Tuple<string, T>> fileList)
        {
            UnityEngine.Object[] fileObjects = new UnityEngine.Object[fileList.Count];
            for (int i = 0; i < fileList.Count; i++)
            {
                string filePath = fileList[i].Item1; // File path is the first item in the tuple

                UnityEngine.Object fileObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                if (fileObject == null) fileObject = GameObject.Find(filePath);

                if (fileObject != null)
                {
                    fileObjects[i] = fileObject;
                    // if (!filePath.Contains("Assets/"))
                    // {
                        EditorGUIUtility.PingObject(fileObject);
                    // }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("File not found: " + filePath);
                }
            }

            Selection.objects = fileObjects;

            return fileObjects;
        }

        public static void HideAllOtherGameObjectInHierarchy(UnityEngine.Object[] selectedGameObjects)
        {
            SceneVisibilityManager.instance.Isolate(selectedGameObjects.OfType<GameObject>().ToArray(), false);
        }
    }
}

#endif
