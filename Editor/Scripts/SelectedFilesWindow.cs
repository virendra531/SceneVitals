#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SelectedFilesWindow : EditorWindow
{
    private VisualElement rootElement;
    private ScrollView scrollView;
    private UnityEngine.Object[] selectedFiles = null;

    [MenuItem("Tools/Selected Files")]
    public static void ShowWindow()
    {
        SelectedFilesWindow window = GetWindow<SelectedFilesWindow>("Selected Files");
        window.selectedFiles = Selection.objects;
        window.Show();
        window.RefreshWindow();
    }

    private void OnEnable()
    {
        rootElement = rootVisualElement;
        scrollView = new ScrollView();
        rootElement.Add(scrollView);

        RefreshWindow();
    }

    private void RefreshWindow()
    {
        scrollView.Clear();

        if (selectedFiles != null && selectedFiles.Length > 0)
        {
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.paddingBottom = 10;
            container.style.paddingRight = 10;
            scrollView.Add(container);

            foreach (UnityEngine.Object fileObject in selectedFiles)
            {
                Texture2D previewTexture = AssetPreview.GetAssetPreview(fileObject);
                if (previewTexture == null)
                    previewTexture = AssetPreview.GetMiniThumbnail(fileObject);

                if (previewTexture != null)
                {
                    VisualElement previewContainer = new VisualElement();
                    previewContainer.style.marginRight = 10;
                    previewContainer.style.marginBottom = 10;
                    container.Add(previewContainer);

                    Image previewElement = new Image() { image = previewTexture, style = { width = 80, height = 80 } };
                    previewElement.RegisterCallback<MouseUpEvent>((evt) => {
                        if (Event.current.shift)
                        {
                            // Add to the selection
                            Object[] currentSelection = Selection.objects;
                            Object[] newSelection = new Object[currentSelection.Length + 1];
                            currentSelection.CopyTo(newSelection, 0);
                            newSelection[newSelection.Length - 1] = fileObject;
                            Selection.objects = newSelection;
                        }
                        else if (Event.current.control || Event.current.command)
                        {
                            // Toggle the selection
                            Object[] currentSelection = Selection.objects;
                            bool isSelected = false;
                            foreach (Object selectedObject in currentSelection)
                            {
                                if (selectedObject == fileObject)
                                {
                                    isSelected = true;
                                    break;
                                }
                            }
                            if (isSelected)
                            {
                                Object[] newSelection = new Object[currentSelection.Length - 1];
                                int index = 0;
                                foreach (Object selectedObject in currentSelection)
                                {
                                    if (selectedObject != fileObject)
                                    {
                                        newSelection[index] = selectedObject;
                                        index++;
                                    }
                                }
                                Selection.objects = newSelection;
                            }
                            else
                            {
                                Object[] newSelection = new Object[currentSelection.Length + 1];
                                currentSelection.CopyTo(newSelection, 0);
                                newSelection[newSelection.Length - 1] = fileObject;
                                Selection.objects = newSelection;
                            }
                        }
                        else
                        {
                            // Select only the clicked object
                            Selection.objects = new Object[] { fileObject };
                        }
                    });
                    previewContainer.Add(previewElement);

                    string fileName = fileObject.name;
                    int lastIndex = fileName.LastIndexOf("/");
                    string displayName = lastIndex >= 0 ? fileName.Substring(lastIndex + 1) : fileName;

                    Label labelElement = new Label(displayName)
                    {
                        style =
                        {
                            fontSize = 12,
                            unityFontStyleAndWeight = FontStyle.Normal,
                            maxWidth = 80,
                            whiteSpace = WhiteSpace.Normal,
                            textOverflow = TextOverflow.Ellipsis,
                            alignSelf = Align.Auto
                        }
                    };
                    previewContainer.Add(labelElement);
                }
                else if (AssetPreview.IsLoadingAssetPreviews())
                {
                    VisualElement loadingElement = new Label("Loading...")
                    {
                        style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, marginBottom = 10 }
                    };
                    container.Add(loadingElement);
                }
                else
                {
                    VisualElement fileElement = new Label(fileObject.name)
                    {
                        style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, marginBottom = 10 }
                    };
                    container.Add(fileElement);
                }
            }
        }
        else
        {
            rootElement.Add(new Label("No files are currently selected.")
            {
                style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold }
            });
        }
    }
}
#endif
