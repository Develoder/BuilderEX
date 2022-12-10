using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class LevelBuilder : EditorWindow
{
    private const string _path = "Assets/Editor Resources/Buildings";
    private const int _rayLayerMask = 1 << 6; // Ground
    
    private Vector2 _scrollPosition;
    private int _selectedElement;
    private int _lastSelectedElement;
    private List<GameObject> _catalog = new List<GameObject>();
    private bool _building;

    private GameObject _createdObject;
    private GameObject[] _parents = new GameObject[3]; // 0 - Gound, 1 - Buildings, 2 - Environments
    private GameObject _previewObject;

    [MenuItem("Level/Builder")]
    private static void ShowWindow()
    {
        GetWindow(typeof(LevelBuilder));
    }

    private void OnFocus()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        RefreshCatalog();
    }

    private void OnGUI()
    {
        DrawParents();
        
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (_createdObject != null)
        {
            EditorGUILayout.LabelField("Created Object Settings");
            Transform createdTransform = _createdObject.transform;
            createdTransform.position = EditorGUILayout.Vector3Field("Position", createdTransform.position);
            createdTransform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Position", createdTransform.rotation.eulerAngles));
            createdTransform.localScale = EditorGUILayout.Vector3Field("Position", createdTransform.localScale);
        }
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        _building = GUILayout.Toggle(_building, "Start building", "Button", GUILayout.Height(60));
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginVertical(GUI.skin.window);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawCatalog(GetCatalogIcons());
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        CheckChangeItemCatalog();
    }

   private void OnSceneGUI(SceneView sceneView)
    {
        if (!_building)
        {
            if (_previewObject != null)
                DestroyPreviewObject();
            return;
        }
        if (!Raycast(out Vector3 contactPoint)) 
            return;
        
        //DrawPointer(contactPoint, Color.red);
        DrawPreview(contactPoint);

        if (CheckInput())
        {
            CreateObject(contactPoint);
        }

        sceneView.Repaint();
    }
   
   private void DrawParents()
   {
       EditorGUILayout.LabelField("Parents");
       
       EditorGUILayout.BeginVertical(GUI.skin.box);
       _parents[0] = (GameObject)EditorGUILayout.ObjectField("Ground", _parents[0], typeof(GameObject), true);
       _parents[1] = (GameObject)EditorGUILayout.ObjectField("Buildings", _parents[1], typeof(GameObject), true);
       _parents[2] = (GameObject)EditorGUILayout.ObjectField("Environments", _parents[2], typeof(GameObject), true);
       EditorGUILayout.EndVertical();
   }
    

    private bool Raycast(out Vector3 contactPoint)
    {
        Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        contactPoint = Vector3.zero;

        if (Physics.Raycast(guiRay, out RaycastHit raycastHit, Mathf.Infinity, _rayLayerMask))
        {
            contactPoint = raycastHit.point;
            return true;
        }
        
        if (_previewObject != null)
            _previewObject.transform.position = Vector3.up * int.MaxValue;
        return false;
    }

    private void DrawPointer(Vector3 position, Color color)
    {
        Handles.color = color;
        Handles.DrawWireCube(position, Vector3.one);
    }
    
    private void DrawPreview(Vector3 contactPoint)
    {
        if (_previewObject == null)
            CreatePreviewObject();

        _previewObject.transform.position = contactPoint;
    }

    private void CheckChangeItemCatalog()
    {
        if (_lastSelectedElement == _selectedElement)
            return;
        
        if(_previewObject != null)
            DestroyImmediate(_previewObject);
        
        CreatePreviewObject();
    }

    private void CreatePreviewObject()
    {
        _lastSelectedElement = _selectedElement;
        _previewObject = Instantiate(_catalog[_selectedElement]);
        _previewObject.transform.name = "PreviewObject";
    }

    private void DestroyPreviewObject()
    {
        DestroyImmediate(_previewObject);
    }

    private bool CheckInput()
    {
        HandleUtility.AddDefaultControl(0);

        return Event.current.type == EventType.MouseDown && Event.current.button == 0;
    }

    private void CreateObject(Vector3 position)
    {
        if (_selectedElement >= _catalog.Count) return;
        
        GameObject prefab = _catalog[_selectedElement];

        _createdObject = Instantiate(prefab, _parents[1].transform, true);
        _createdObject.transform.position = position;

        Undo.RegisterCreatedObjectUndo(_createdObject, "Create Building");
    }

    private void DrawCatalog(List<GUIContent> catalogIcons)
    {
        GUILayout.Label("Buildings");
        _selectedElement = GUILayout.SelectionGrid(_selectedElement, catalogIcons.ToArray(), 4, GUILayout.Width(400), GUILayout.Height(1000));
    }

    private List<GUIContent> GetCatalogIcons()
    {
        List<GUIContent> catalogIcons = new List<GUIContent>();

        foreach (var element in _catalog)
        {
            Texture2D texture = AssetPreview.GetAssetPreview(element);
            catalogIcons.Add(new GUIContent(texture));
        }

        return catalogIcons;
    }

    private void RefreshCatalog()
    {
        _catalog.Clear();

        System.IO.Directory.CreateDirectory(_path);
        string[] prefabFiles = System.IO.Directory.GetFiles(_path, "*.prefab");
        foreach (var prefabFile in prefabFiles)
            _catalog.Add(AssetDatabase.LoadAssetAtPath(prefabFile, typeof(GameObject)) as GameObject);
    }
}