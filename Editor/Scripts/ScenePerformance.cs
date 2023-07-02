#if UNITY_EDITOR
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.Profiling;
using System.Linq;
using System.IO;
using System;

namespace SceneVitals
{
    public class PackageSizeResponse
    {
        public string packageName;
        public string packagePath;
        public int packageSizeMB;
    }

    public class PerformanceResponse
    {
        private const int m = 1000000;
        private const int k = 1000;

        public static readonly int MAX_SUGGESTED_VERTS = 500 * k;
        public static readonly int MAX_SUGGESTED_UNIQUE_MATERIALS = 75;
        public static readonly int MAX_SUGGESTED_SHARED_TEXTURE_MB = 200;
        public static readonly int MAX_SUGGESTED_COLLIDER_VERTS = 75 * k;

        public string sceneName;
        public string scenePath;

        public bool hasLightmaps => lightmapTextureMB > 0;
        public bool hasLightprobes;
        public bool hasReflectionProbes;

        public int lightmapTextureMB;
        public int verts;
        public int uniqueVerts;
        public int uniqueMaterials;
        public int materialTextureMB;
        public int meshColliderVerts;
        public int realtimeLights;
        public int reflectionProbeMB;//textures

        // Per asset data about their size
        public IReadOnlyList<Tuple<string, int>> meshVertCounts;
        public IReadOnlyList<Tuple<string, int>> meshColliderVertCounts;
        public IReadOnlyList<Tuple<string, float>> textureMemorySizesMB;

        public int sharedTextureMB => materialTextureMB + lightmapTextureMB + reflectionProbeMB;

        //how long it took to analyze the scene.
        //used in sceneVitals to auto adjust the refresh rate.
        public float responseMiliseconds;

        public float vertPercent => (float)verts / MAX_SUGGESTED_VERTS;
        public float uniqueMaterialsPercent => (float)uniqueMaterials / MAX_SUGGESTED_UNIQUE_MATERIALS;
        public float sharedTexturePercent => (float)sharedTextureMB / MAX_SUGGESTED_SHARED_TEXTURE_MB;
        public float meshColliderVertPercent => (float)meshColliderVerts / MAX_SUGGESTED_COLLIDER_VERTS;
    }

    public class ScenePerformance
    {
        // // WIP
        // public static void GetActiveScenePackageResponseSlow()
        // {
        //     //todo to be removed. Counting files one by one is faster that packaging the scene.
        //     PackageSizeResponse response = new PackageSizeResponse();
        //     string tempOutputPath = "Temp/SpatialPackage.unitypackage";
        //     BuildUtility.PackageActiveScene(tempOutputPath);
        //     FileInfo packageInfo = new FileInfo(tempOutputPath);
        //     response.packageSizeMB = (int)packageInfo.Length / 1024 / 1024;
        //     Debug.LogError("estimated package size: " + response.packageSizeMB + "MB");

        //     string scenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        //     string[] assetPaths = AssetDatabase.GetDependencies(scenePath, true);
        //     assetPaths.Append(scenePath);

        //     long bytes = 0;
        //     foreach (string assetPath in assetPaths)
        //     {
        //         Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        //         UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
        //         bytes += Profiler.GetRuntimeMemorySizeLong(asset);
        //         //todo we want to estimate the build size of assets here, not runtime size.
        //     }
        // }

        //Takes usually 1ms or less on sample scene
        public static PerformanceResponse GetActiveScenePerformanceResponse()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var scene = EditorSceneManager.GetActiveScene();

            List<Tuple<string, int>> meshVertCounts = new List<Tuple<string, int>>();
            List<Tuple<string, int>> meshColliderVertCounts = new List<Tuple<string, int>>();
            List<Tuple<string, float>> textureSizesMB = new List<Tuple<string, float>>();

            PerformanceResponse response = new PerformanceResponse();
            response.sceneName = scene.name;
            response.scenePath = scene.path;
            response.meshVertCounts = meshVertCounts;
            response.meshColliderVertCounts = meshColliderVertCounts;
            response.textureMemorySizesMB = textureSizesMB;

            // Count lightmaps size
            long bytes = 0;
            LightmapData[] lightmaps = LightmapSettings.lightmaps;
            foreach (LightmapData lightmap in lightmaps)
            {
                if (lightmap.lightmapColor != null)
                {
                    long sizeInBytes = Profiler.GetRuntimeMemorySizeLong(lightmap.lightmapColor);
                    bytes += sizeInBytes;
                    textureSizesMB.Add(new Tuple<string, float>(AssetDatabase.GetAssetPath(lightmap.lightmapColor), sizeInBytes / 1024f / 1024f));
                }
                if (lightmap.lightmapDir != null)
                {
                    long sizeInBytes = Profiler.GetRuntimeMemorySizeLong(lightmap.lightmapDir);
                    bytes += sizeInBytes;
                    textureSizesMB.Add(new Tuple<string, float>(AssetDatabase.GetAssetPath(lightmap.lightmapDir), sizeInBytes / 1024f / 1024f));
                }
                if (lightmap.shadowMask != null)
                {
                    long sizeInBytes = Profiler.GetRuntimeMemorySizeLong(lightmap.shadowMask);
                    bytes += sizeInBytes;
                    textureSizesMB.Add(new Tuple<string, float>(AssetDatabase.GetAssetPath(lightmap.shadowMask), sizeInBytes / 1024f / 1024f));
                }
            }
            response.lightmapTextureMB = (int)bytes / 1024 / 1024;

            // Count scene object sizes
            List<Texture> foundTextures = new List<Texture>();
            List<Material> materials = new List<Material>();
            List<Mesh> foundMeshes = new List<Mesh>();
            Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                //look for materials and textures
                materials.AddRange(renderer.sharedMaterials);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    foreach (var texName in material.GetTexturePropertyNames())
                    {
                        var tex = material.GetTexture(texName);
                        if (tex != null)
                        {
                            foundTextures.Add(tex);
                        }
                    }
                }
                // look for mesh
                if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        foundMeshes.Add(filter.sharedMesh);
                        response.verts += filter.sharedMesh.vertexCount;
                    }
                }
                else if (renderer is SkinnedMeshRenderer)
                {
                    SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                    if (skinned.sharedMesh != null)
                    {
                        foundMeshes.Add(skinned.sharedMesh);
                        response.verts += skinned.sharedMesh.vertexCount;
                    }
                }
                else if (renderer is BillboardRenderer)
                {
                    response.verts += 4;
                }
            }
            IEnumerable<Mesh> uniqueMeshes = foundMeshes.Distinct();
            meshVertCounts.AddRange(uniqueMeshes.Select(m => new Tuple<string, int>(AssetDatabase.GetAssetPath(m), m.vertexCount)));
            response.uniqueVerts = uniqueMeshes.Distinct().Sum(m => m.vertexCount);
            response.uniqueMaterials = materials.FindAll(m => m != null).Select(m => m.name).Distinct().Count();

            // Count texture sizes
            bytes = 0;
            foreach (Texture texture in foundTextures.Distinct())
            {
                long sizeInBytes = Profiler.GetRuntimeMemorySizeLong(texture);
                bytes += sizeInBytes;
                textureSizesMB.Add(new Tuple<string, float>(AssetDatabase.GetAssetPath(texture), sizeInBytes / 1024f / 1024f));
            }
            response.materialTextureMB = (int)(bytes / 1024 / 1024);

            // Count mesh collider vertices
            MeshCollider[] meshColliders = GameObject.FindObjectsOfType<MeshCollider>(true);
            foreach (MeshCollider meshCollider in meshColliders)
            {
                if (meshCollider.sharedMesh != null)
                {
                    response.meshColliderVerts += meshCollider.sharedMesh.vertexCount;
                    meshColliderVertCounts.Add(new Tuple<string, int>(GetGameObjectPath(meshCollider.gameObject), meshCollider.sharedMesh.vertexCount));
                }
            }

            // Look for light / reflection probes
            LightProbeGroup[] lightProbeGroups = GameObject.FindObjectsOfType<LightProbeGroup>(true);
            foreach (LightProbeGroup lightProbeGroup in lightProbeGroups)
            {
                if (lightProbeGroup.probePositions.Length > 0)
                {
                    response.hasLightprobes = true;
                    break;
                }
            }
            if (GameObject.FindObjectsOfType<ReflectionProbe>(true).Length > 0)
            {
                response.hasReflectionProbes = true;
            }

            // Look for lights
            Light[] lights = GameObject.FindObjectsOfType<Light>(true);
            response.realtimeLights = lights.Where(l => l.lightmapBakeType != LightmapBakeType.Baked).Count();

            bytes = 0;
            ReflectionProbe[] reflectionProbes = GameObject.FindObjectsOfType<ReflectionProbe>(true);
            foreach (ReflectionProbe probe in reflectionProbes)
            {
                if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Baked && probe.texture != null)
                {
                    bytes += Profiler.GetRuntimeMemorySizeLong(probe.texture);
                }
                //realtime probes are currently disabled... but leaving this incase we enable them down the road.
                else if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
                {
                    bytes += probe.resolution * probe.resolution * 3;
                }
            }
            response.reflectionProbeMB = (int)(bytes / 1024 / 1024);

            // Sort by size descending
            meshVertCounts.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            meshColliderVertCounts.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            textureSizesMB.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            timer.Stop();
            response.responseMiliseconds = timer.ElapsedMilliseconds;

            return response;
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
            GetGameObjectRecursive(obj.transform, stringBuilder);
            return stringBuilder.ToString();
        }

        private static void GetGameObjectRecursive(Transform t, System.Text.StringBuilder stringBuilder)
        {
            if (t.parent != null)
                GetGameObjectRecursive(t.parent, stringBuilder);

            stringBuilder.AppendFormat("/{0}", t.gameObject.name);
        }

        public static void TestScenePerformance(Scene scene)
        {
            PerformanceResponse response = GetActiveScenePerformanceResponse();

            if (response.meshColliderVertPercent > 1f)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} has a <color=yellow>lot of high density mesh colliders ({response.meshColliderVerts}/{PerformanceResponse.MAX_SUGGESTED_COLLIDER_VERTS}).</color>\n"
                    + "You should try to use primitives or low density meshes for colliders where possible. \n"
                    + "High density collision geometry will impact the performance of your space.\n"
                    + "Here's a list of all objects(30 selected) with high density mesh colliders:\n - " + "<color=yellow>" + string.Join("\n - ", response.meshColliderVertCounts.Take(30).Select(m => $"<color=red>{m.Item2}</color> - {m.Item1}")) + "</color>\n"
                );
            }

            if (!response.hasLightmaps)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} <color=yellow>doesn't have lightmaps</color>.\n"
                    + "It is highly recommended that you bake lightmaps in each scene. This will greatly improve the fidelity of your space.\n"
                );
            }

            if (!response.hasLightprobes)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} <color=yellow>doesn't have light probes.</color>\n"
                    + "It is highly recommended that you bake light probes in each scene. This will allow game objects to interact with the baked lights in your space properly.\n"
                );
            }

            //skipping reflection probe warning. Not everyone will benefit much from them.

            if (!response.hasReflectionProbes)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} <color=yellow>doesn't have reflection probes.</color>\n"
                    + "It is highly recommended that you add reflection probes to each scene. This will allow game objects to interact with the baked reflections in your space properly.\n"
                );
            }

            if (response.vertPercent > 1f)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} has <color=yellow>too many vertices.</color>\n"
                    + "The scene has too many high detail models. It is recommended that you stay within the suggested limits or your asset may not perform well on all platforms.\n"
                    + "Here's a list of all objects(30 selected) with high vertex counts:\n - " + "<color=yellow>" + string.Join("\n - ", response.meshVertCounts.Take(30).Select(m => $"<color=red>{m.Item2}</color> - {m.Item1}")) + "</color>\n"
                );
            }

            if (response.uniqueMaterialsPercent > 1f)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} has <color=yellow>too many unique materials.</color>\n"
                    + $"It is encouraged for scenes to limit unique materials to around {PerformanceResponse.MAX_SUGGESTED_UNIQUE_MATERIALS}. \n"
                    + "The more unique materials you have, the less likely it is that your asset will perform well on all platforms. \n"
                    + "Look into texture atlasing techniques to share textures and materials across multiple separate objects.\n"
                );
            }

            if (response.sharedTexturePercent > 1f)
            {
                UnityEngine.Debug.Log(
                    $"Scene {scene.name} has <color=yellow>too many shared textures.</color>\n"
                    + $"For WebGL it is encouraged to be be limited to {PerformanceResponse.MAX_SUGGESTED_SHARED_TEXTURE_MB} MB of shared textures. \n"
                    + "High memory usage can cause application crashes on lower end devices. It is highly recommended that you stay within the suggested limits. \n"
                    + "Compressing your textures will help reduce their size.\n"
                    + "Here's a list of all textures(40 selected) used by the scene:\n - " + "<color=yellow>" + string.Join("\n - ", response.textureMemorySizesMB.Take(40).Select(m => $"<color=red>{m.Item2:0.00}MB</color> - {m.Item1}")) + "</color>\n"
                );

                // SelectTextureFiles(response.textureMemorySizesMB.Take(40).ToList());
                // SelectedFilesWindow.ShowWindow();
            }
        }
    }
}
#endif