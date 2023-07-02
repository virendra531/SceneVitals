#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;

namespace SceneVitals
{
    [Overlay(typeof(SceneView), "Scene Vitals", true)]
    [Icon("Assets/SceneVitals/Icon/icon_sceneVitals.png")]
    public class SceneVitalsOverlay : Overlay, ITransientOverlay
    {
        private const string resourcesPath = "Packages/com.virendra531.scenevitals/";
        private const string BASE_BLOCK_CLASS = "InfoBlock";
        private const string BASE_SUB_BLOCK_CLASS = "SubBlock";
        private const string GREEN_BLOCK_CLASS = "InfoBlock_green";
        private const string YELLOW_BLOCK_CLASS = "InfoBlock_yellow";
        private const string RED_BLOCK_CLASS = "InfoBlock_red";

        // Controls whether the panel or the docked button is visible.
        bool ITransientOverlay.visible => true;

        private VisualElement _verticesBlock;
        private Label _verticesCount;
        private Label _verticesMax;

        //For some reason image tint does not get passed to child elements like text color.
        //So we need to assign the color class to each of these icons individually.
        private VisualElement _meshIcon;
        private VisualElement _textureIcon;
        private VisualElement _materialIcon;

        private VisualElement _sharedTexturesBlock;
        private VisualElement _sharedTexturesSubBlock;
        private Label _sharedTexturesCount;
        private Label _sharedTexturesMax;

        private Label _materialTexturesCount;
        private Label _lightmapTexturesCount;
        private VisualElement _reflectionProbeBlock;
        private Label _reflectionProbeCount;

        private VisualElement _materialsBlock;
        private Label _materialsCount;
        private Label _materialsMax;

        private VisualElement _noLightmapsWarning;
        private VisualElement _noLightprobesWarning;
        private VisualElement _highCollisionMeshWarning;

        private double _lastRefreshTime = -1.0;
        private float _autoRefreshEvery = 30f;
        private bool _addedRefreshEvents = false;

        public override VisualElement CreatePanelContent()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{resourcesPath}/Editor/UI/SceneVitals.uxml");
            VisualElement element = visualTree.Instantiate();
            var root = new VisualElement() { name = "My Toolbar Root" };
            root.Add(element);
            InitializeElements(root);
            UpdatePerformanceStats();

            // This function can get called multiple times (e.g. closing and opening a docked panel). We only need to subscribe once.
            if (!_addedRefreshEvents)
            {
                // We refresh any time a scene is opened/saved, and periodically.
                // I didn't want to do onDirty because I thought it would be too spammy.
                EditorApplication.update += AutoRefreshTimer;
                EditorSceneManager.sceneOpened += (scene, mode) => UpdatePerformanceStats();
                EditorSceneManager.sceneSaved += (scene) => UpdatePerformanceStats();
                _addedRefreshEvents = true;
            }

            return root;
        }

        private void AutoRefreshTimer()
        {
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > _autoRefreshEvery)
            {
                UpdatePerformanceStats();
            }
        }

        private void InitializeElements(VisualElement root)
        {
            _meshIcon = root.Q("MeshIcon");
            _textureIcon = root.Q("TextureIcon");
            _materialIcon = root.Q("MaterialIcon");

            _verticesBlock = root.Q("Vertices");
            _verticesCount = root.Query<Label>("VerticesCount").First();
            _verticesMax = root.Query<Label>("VerticesMax").First();

            _sharedTexturesBlock = root.Q("SharedTextures");
            _sharedTexturesSubBlock = root.Q("SharedTexturesSubBlock");
            _sharedTexturesCount = root.Query<Label>("SharedTexturesCount").First();
            _sharedTexturesMax = root.Query<Label>("SharedTexturesMax").First();
            _materialTexturesCount = root.Query<Label>("MaterialTexturesCount").First();
            _lightmapTexturesCount = root.Query<Label>("LightmapTexturesCount").First();
            _reflectionProbeBlock = root.Q("ReflectionProbes");
            _reflectionProbeCount = root.Query<Label>("ReflectionProbesCount").First();

            _materialsBlock = root.Q("Materials");
            _materialsCount = root.Query<Label>("MaterialsCount").First();
            _materialsMax = root.Query<Label>("MaterialsMax").First();

            _noLightmapsWarning = root.Q("NoLightmapsWarning");
            _noLightprobesWarning = root.Q("NoLightprobesWarning");
            _highCollisionMeshWarning = root.Q("HighCollisionMeshWarning");
        }

        private void UpdatePerformanceStats()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            PerformanceResponse resp = ScenePerformance.GetActiveScenePerformanceResponse();

            // Change the refresh frequency based on how long the request takes, since it can affect performance of large scenes and slow computers.
            _autoRefreshEvery = Mathf.Clamp(resp.responseMiliseconds * 5f, 5f, 100f);

            SetBaseClass(_verticesBlock);
            SetBlockClassFromRatio(_verticesBlock, resp.vertPercent);
            _verticesCount.text = AbbreviateNumber(resp.verts);
            _verticesMax.text = "/ " + AbbreviateNumber(PerformanceResponse.MAX_SUGGESTED_VERTS);
            _meshIcon.ClearClassList();
            SetBlockClassFromRatio(_meshIcon, resp.vertPercent);

            SetBaseClass(_sharedTexturesBlock);
            SetBaseClass(_sharedTexturesSubBlock, true);
            SetBlockClassFromRatio(_sharedTexturesBlock, resp.sharedTexturePercent);
            SetBlockClassFromRatio(_sharedTexturesSubBlock, resp.sharedTexturePercent);
            _sharedTexturesCount.text = AbbreviateSize(resp.sharedTextureMB);
            _sharedTexturesMax.text = "/ " + AbbreviateSize(PerformanceResponse.MAX_SUGGESTED_SHARED_TEXTURE_MB);
            _materialTexturesCount.text = AbbreviateSize(resp.materialTextureMB);
            _lightmapTexturesCount.text = AbbreviateSize(resp.lightmapTextureMB);
            _reflectionProbeBlock.style.display = resp.reflectionProbeMB > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _reflectionProbeCount.text = AbbreviateSize(resp.reflectionProbeMB);
            _textureIcon.ClearClassList();
            SetBlockClassFromRatio(_textureIcon, resp.sharedTexturePercent);

            SetBaseClass(_materialsBlock);
            SetBlockClassFromRatio(_materialsBlock, resp.uniqueMaterialsPercent);
            _materialsCount.text = AbbreviateNumber(resp.uniqueMaterials);
            _materialsMax.text = "/ " + AbbreviateNumber(PerformanceResponse.MAX_SUGGESTED_UNIQUE_MATERIALS);
            _materialIcon.ClearClassList();
            SetBlockClassFromRatio(_materialIcon, resp.uniqueMaterialsPercent);

            _noLightmapsWarning.style.display = resp.hasLightmaps ? DisplayStyle.None : DisplayStyle.Flex;
            //show if we have lightmaps but no light probes
            _noLightprobesWarning.style.display = resp.hasLightprobes || !resp.hasLightmaps ? DisplayStyle.None : DisplayStyle.Flex;
            _highCollisionMeshWarning.style.display = resp.meshColliderVertPercent < 1f ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void SetBaseClass(VisualElement element, bool isSubBlock = false)
        {
            element.ClearClassList();
            element.AddToClassList(isSubBlock ? BASE_SUB_BLOCK_CLASS : BASE_BLOCK_CLASS);
        }

        private void SetBlockClassFromRatio(VisualElement element, float ratio)
        {
            if (ratio > 1f)
            {
                element.AddToClassList(RED_BLOCK_CLASS);
            }
            else if (ratio > .6f)
            {
                element.AddToClassList(YELLOW_BLOCK_CLASS);
            }
            else
            {
                element.AddToClassList(GREEN_BLOCK_CLASS);
            }
        }

        // Thousand(K), Million(M), Billion(B)
        public string AbbreviateNumber(int number)
        {
            if (number < 1000)
            {
                return number.ToString();
            }
            else if (number < 10000)
            {
                return (number / 1000f).ToString("0.#") + "K";
            }
            else if (number < 1000000)
            {
                return (number / 1000).ToString() + "K";
            }
            else if (number < 10000000)
            {
                return (number / 1000000f).ToString("0.#") + "M";
            }
            else if (number < 1000000000)
            {
                return (number / 1000000).ToString() + "M";
            }
            else
            {
                return (number / 1000000000f).ToString("0.#") + "B";
            }
        }


        // Thousand(K), Million(M), Billion(B)
        // public string AbbreviateNumber(int number)
        // { 
        //     if (number < 1000)
        //     {
        //         return number.ToString();
        //     }
        //     else if (number < 1000000)
        //     {
        //         return (number / 1000f).ToString("0.#") + "K";
        //     }
        //     else if (number < 1000000000)
        //     {
        //         return (number / 1000000f).ToString("0.#") + "M";
        //     }
        //     else
        //     {
        //         return (number / 1000000000f).ToString("0.#") + "B";
        //     }
        // }

        // Bytes(Bytes), KiloByte(KB), MegaByte(MB), GigaByte(GB)
        public string AbbreviateSize(int number)
        {
            number = number * 1000000;
            if (number < 1000)
            {
                return number.ToString() + "Byte";
            }
            else if (number < 1000000)
            {
                return (number / 1000f).ToString("0.#") + "KB";
            }
            else if (number < 1000000000)
            {
                return (number / 1000000f).ToString("0.#") + "MB";
            }
            else
            {
                return (number / 1000000000f).ToString("0.#") + "GB";
            }
        }
    }
}
#endif
