using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

public class LevelBuilder : EditorWindow
{
    private const string _path = "Assets/Editor Resources/";
    
    private const int _tileLayerMask = 6; 
    private const int _groundLayerMask = 7; 
    private const int _buildingsLayerMask = 8; 
    private const int _defaultLayerMask = 0; 
    private static readonly int[] _layerMasks = new int[] { _tileLayerMask, _groundLayerMask, _buildingsLayerMask, _defaultLayerMask };
    private const int _gridSize = 30;

    private Vector2 _scrollPosition;
    private int _selectedElement;
    private int _lastSelectedElement;
    private int _lastSelectedTool;
    private List<GameObject> _catalog = new List<GameObject>();
    private bool _building;

    private GameObject _createdObject;
    private GameObject[] _parents = new GameObject[3]; // 0 - Ground, 1 - Buildings, 2 - Environments
    private GameObject _previewObject;

    private Construction _selectedConstruction = Construction.Ground;
    private string[] _tabNames = new string[] {"Ground", "Buildings", "Environments"};
    private string[] _nameFolderTab = new string[] { "Ground", "Buildings", "Environments" };

    [MenuItem("Level/Builder")]
    private static void ShowWindow()
    {
        GetWindow(typeof(LevelBuilder));
    }

    private void OnFocus()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        //RefreshCatalog();
    }

    private void OnGUI()
    {
        DrawParents();
        
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        _building = GUILayout.Toggle(_building, "Start building", "Button", GUILayout.Height(60));
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        int selectedTool = GUILayout.Toolbar((int)_selectedConstruction, _tabNames);
        EditorGUILayout.BeginVertical(GUI.skin.window);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawCatalog(GetCatalogIcons());
            
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        Construction currentConstruction = (Construction)Enum.ToObject(typeof(Construction), selectedTool);

        if (selectedTool != _lastSelectedTool)
        {
            _lastSelectedTool = selectedTool;
            _building = false;
        }

        if (currentConstruction != _selectedConstruction)
        {
            _selectedConstruction = currentConstruction;
            rootVisualElement.Clear();
            RefreshCatalog();
        }

        
        if (_previewObject != null)
            //DrawObjectSettings();
            EditionParametersPreview();
        
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

        DrawPreview(contactPoint);
        DrawPointer(contactPoint);
        
        if (_selectedConstruction == Construction.Ground)
        {
            // ???????? ???????????? ?????? ??????????
            if(CheckAllow(contactPoint))
                if (CheckInput())
                    CreateObject(contactPoint);
        }
        else if(_selectedConstruction == Construction.Buildings)
        {
            //DrawPointer(contactPoint, Color.red);
            if(CheckAllow(contactPoint))
                if (CheckInput())
                    CreateObject(contactPoint);
        }
        else
        {
            if (CheckInput())
                CreateObject(contactPoint);
        }
        
        sceneView.Repaint();
    }
   
   private void DrawParents()
   {
       EditorGUILayout.LabelField("Parents");
       
       EditorGUILayout.BeginVertical(GUI.skin.box);
       int enemyTypesCount = Enum.GetNames(typeof(Construction)).Length;
       for (int i = 0; i < enemyTypesCount; i++)
       {
           Construction type = (Construction)Enum.ToObject(typeof(Construction), i);
           _parents[i] = (GameObject)EditorGUILayout.ObjectField(type.ToString(), _parents[i], typeof(GameObject), true);
       }
       
       EditorGUILayout.EndVertical();
   }

   private bool Raycast(out Vector3 contactPoint)
    {
        Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        contactPoint = Vector3.zero;
        
        if (Physics.Raycast(guiRay, out RaycastHit raycastHit, Mathf.Infinity, GetLayerMask(true)))
        {
            if(_selectedConstruction == Construction.Ground)    //???????? ????????????, ???? ???? ??????????.
            {
                contactPoint = raycastHit.point;
                float contactPointX = raycastHit.point.x - (raycastHit.point.x % _gridSize); 
                float contactPointZ = raycastHit.point.z - (raycastHit.point.z % _gridSize); 
                contactPoint = new Vector3(contactPointX, 0, contactPointZ);
                return true;
            }
            else
            {
                contactPoint = raycastHit.point;
                return true;
            }
        }
        
        if (_previewObject != null)
            _previewObject.transform.position = Vector3.up * int.MaxValue;
        return false;
    }

    private void DrawPointer(Vector3 position)
    {
        if(CheckAllow(position))
        {
            Handles.color = Color.white;
        }
        else
        {
            Handles.color = Color.red;
            HandleUtility.AddDefaultControl(0);
        }

        Mesh mesh = _previewObject.GetComponentsInChildren<MeshFilter>()[0].sharedMesh;
        Vector3 meshSize = mesh.bounds.size * _previewObject.transform.localScale.x;
        meshSize.y = 0;
        Handles.DrawWireCube(position, meshSize);
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
        GameObject parent = _parents[(int)_selectedConstruction];
        
        _createdObject = Instantiate(prefab, parent.transform, true);
        _createdObject.transform.position = position;
        _createdObject.layer = GetLayer();
        
        _createdObject.transform.rotation = _previewObject.transform.rotation;
        _createdObject.transform.localScale = _previewObject.transform.localScale;

        Undo.RegisterCreatedObjectUndo(_createdObject, "Create Building");
    }
    
    private void DrawObjectSettings()
    {
        EditorGUILayout.LabelField("Created Object Settings");
        Transform createdTransform = _createdObject.transform;
        //createdTransform.position = EditorGUILayout.Vector3Field("Position", createdTransform.position);

        createdTransform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Rotation", createdTransform.rotation.eulerAngles));
        createdTransform.localScale = EditorGUILayout.Vector3Field("Scale", createdTransform.localScale);
    }

    private void EditionParametersPreview()
    {
        if (Event.current.type != EventType.KeyDown)
            return;

        switch (_selectedConstruction)
        {
            case Construction.Ground:
                EditionParametersPreviewGround();
                break;
            case Construction.Buildings:
                EditionParametersPreviewBuildings();
                break;
            case Construction.Environments:
                EditionParametersPreviewBuildings();
                break;
        }
    }

    private void EditionParametersPreviewGround()
    {
        switch (Event.current.keyCode)
        {
            //?????????????? ??????????
            case KeyCode.Alpha5:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.up, 90);
                break;
            //?????????????? ????????????
            case KeyCode.Alpha6:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.down, 90);
                break;
        }
    }
    
    private void EditionParametersPreviewBuildings()
    {
        switch (Event.current.keyCode)
        {
            case KeyCode.Alpha4:
                _previewObject.transform.localScale += Vector3.one * 0.1f;
                break;

            case KeyCode.Alpha3:
                _previewObject.transform.localScale -= Vector3.one * 0.1f;
                if (_previewObject.transform.localScale.x < 0.05f)
                    _previewObject.transform.localScale = Vector3.one * 0.05f;
                break;

            //?????????????? ??????????
            case KeyCode.Alpha5:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.up, 20);
                break;
            //?????????????? ????????????
            case KeyCode.Alpha6:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.down, 20);
                break;
        }
    }

    private void DrawCatalog(List<GUIContent> catalogIcons)
    {
        GUILayout.Label(_selectedConstruction.ToString());

        float targetSizeElement = 100f; // ?????????????????? ???????????? ??????????????????
        float minSizeElement = 50f; // ?????????????????????? ???????????? ????????????????
        float paddingLeft = 30f; // ???????????? ?? ????????, ???????? ?????????????? ???? ??????????????????????
        float withWindow = position.width; // ???????????? ????????
        int column = Mathf.RoundToInt((withWindow - withWindow % targetSizeElement) / targetSizeElement); // ?????????????? ?????????????????????? ????????????????
        column = Mathf.Clamp(column, 1, int.MaxValue); // ???? ???????? ?????????????????????? ???????????????? ?????????????????? ????????
        float sizeElement = targetSizeElement + // ?? ???????????????????? ?????????????? ?????????????????? ??????????????
            (withWindow - column * targetSizeElement) / column // ???????????????????? ?????????????? ???? ???????????????? ???????????????????????? ???????????????????? ????????????????
            - paddingLeft / column; // ?????????? ???????????? ???? ???????? ??????????????????
        sizeElement = Mathf.Clamp(sizeElement, minSizeElement, float.MaxValue); // ?????????????? ?????????? ???????????? ???????????????????????? ???????????????? ???? ????????
        int row = (catalogIcons.Count - catalogIcons.Count % column) / column; // ?????????????????????? ?????????????????????? ??????????
        
        float with = sizeElement * column;
        float height = row * sizeElement;
        
        _selectedElement = GUILayout.SelectionGrid(_selectedElement, catalogIcons.ToArray(), column, GUILayout.Width(with), GUILayout.Height(height));
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

        System.IO.Directory.CreateDirectory(FullPath());
        string[] prefabFiles = System.IO.Directory.GetFiles(FullPath(), "*.prefab");
        foreach (var prefabFile in prefabFiles)
            _catalog.Add(AssetDatabase.LoadAssetAtPath(prefabFile, typeof(GameObject)) as GameObject);
    }

    private string FullPath()
    {
        //Debug.Log($"{_selectedConstruction} {(int)_selectedConstruction}");
        return _path + _nameFolderTab[(int)_selectedConstruction];
    }
    

    private bool CheckAllow(Vector3 position)
    {
        Quaternion rotation = _previewObject.transform.rotation;
        Mesh mesh = _catalog[_selectedElement].GetComponentsInChildren<MeshFilter>()[0].sharedMesh;
        Vector3 meshSize = mesh.bounds.size * _previewObject.transform.localScale.x / (_selectedConstruction == Construction.Ground ? 10 : 2);
        Collider[] colliders = Physics.OverlapBox(position, meshSize, rotation, GetLayerMask(false));
        
        return colliders.Length == 0;
    }

    private int GetLayerMask(bool forRay)
    {
        if (_selectedConstruction == Construction.Environments)
            return forRay ? (1 << _groundLayerMask) : 0;
        return 1 << _layerMasks[Convert.ToInt32(!forRay) + (int)_selectedConstruction];
    }

    private int GetLayer()
    {
        if (_selectedConstruction == Construction.Environments)
            return _defaultLayerMask;
        return 1 + _layerMasks[(int)_selectedConstruction];
    }
}