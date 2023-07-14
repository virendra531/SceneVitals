#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SelectedFilesWindow : EditorWindow
{
    private VisualElement rootElement;
    private ScrollView scrollView;
    private UnityEngine.Object[] selectedFiles = null;

    private Vector2 startDragPosition;
    private Rect dragSelectionRect;
    private bool isDragging = false;

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

        rootElement.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
        rootElement.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
        rootElement.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);

        RefreshWindow();
    }

    private void OnMouseDownEvent(MouseDownEvent evt)
    {
        if (evt.button == 0 && !evt.ctrlKey && !evt.shiftKey)
        {
            startDragPosition = evt.mousePosition;
            dragSelectionRect = new Rect(startDragPosition, Vector2.zero);
            isDragging = true;
            evt.StopPropagation();
        }
    }

    private void OnMouseUpEvent(MouseUpEvent evt)
    {
        if (evt.button == 0 && isDragging)
        {
            isDragging = false;
            evt.StopPropagation();

            SelectFilesInRect(dragSelectionRect);
            Repaint();
        }
    }

    private void OnMouseMoveEvent(MouseMoveEvent evt)
    {
        if (isDragging)
        {
            dragSelectionRect.size = evt.mousePosition - startDragPosition;
            evt.StopPropagation();
            Repaint();
        }
    }

    private void SelectFilesInRect(Rect selectionRect)
    {
        if (selectedFiles == null)
            return;

        bool selectionChanged = false;
        bool isShiftPressed = Event.current.shift;

        for (int i = 0; i < selectedFiles.Length; i++)
        {
            UnityEngine.Object fileObject = selectedFiles[i];

            Vector2 objectPosition = GetObjectPosition(i);

            Rect objectRect = new Rect(objectPosition, new Vector2(80, 80)); // Assuming each object has a size of 80x80

            if (selectionRect.Overlaps(objectRect))
            {
                if (!isShiftPressed && !Selection.Contains(fileObject))
                {
                    Selection.objects = new Object[] { fileObject };
                    selectionChanged = true;
                }
                else if (isShiftPressed && !Selection.Contains(fileObject))
                {
                    Object[] newSelection = new Object[Selection.objects.Length + 1];
                    Selection.objects.CopyTo(newSelection, 0);
                    newSelection[newSelection.Length - 1] = fileObject;
                    Selection.objects = newSelection;
                    selectionChanged = true;
                }
            }
            else
            {
                if (isShiftPressed && Selection.Contains(fileObject))
                {
                    Object[] newSelection = new Object[Selection.objects.Length - 1];
                    int index = 0;
                    foreach (Object selectedObject in Selection.objects)
                    {
                        if (selectedObject != fileObject)
                        {
                            newSelection[index] = selectedObject;
                            index++;
                        }
                    }
                    Selection.objects = newSelection;
                    selectionChanged = true;
                }
            }
        }

        if (selectionChanged)
            EditorGUIUtility.PingObject(Selection.activeObject);
    }

    private Vector2 GetObjectPosition(int index)
    {
        int colCount = 5;
        int rowCount = Mathf.CeilToInt((float)selectedFiles.Length / colCount);
        int row = index / colCount;
        int col = index % colCount;
        float posX = col * 100;
        float posY = row * 100;
        return new Vector2(posX, posY);
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

            for (int i = 0; i < selectedFiles.Length; i++)
            {
                UnityEngine.Object fileObject = selectedFiles[i];

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
                    previewElement.RegisterCallback<MouseUpEvent>((evt) =>
                    {
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
