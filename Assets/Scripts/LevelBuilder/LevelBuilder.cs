using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public class LevelBuilder : EditorWindow
{
    private const string _path = "Assets/Editor Resources/";
    
    private const int _groundLayerMask = 1 << 6; // Ground
    private const int _TileLayerMask = 1 << 7; // Buildings
    private const int _buildingsLayerMask = 1 << 8; // Buildingsprivate
    private static int[] _layerMasks = new int[] { _TileLayerMask, _groundLayerMask, _buildingsLayerMask };
    private const int _gridSize = 10;

    private Vector2 _scrollPosition;
    private int _selectedElement;
    private int _lastSelectedElement;
    private List<GameObject> _catalog = new List<GameObject>();
    private bool _building;

    private GameObject _createdObject;
    private GameObject[] _parents = new GameObject[3]; // 0 - Ground, 1 - Buildings, 2 - Environments
    private GameObject _previewObject;
    // private Vector3 _previewScale = Vector3.zero;
    // private Quaternion _previewRotation = Quaternion.identity;

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
        
        int selectedTool = GUILayout.Toolbar((int)_selectedConstruction, _tabNames);
        Construction currentConstruction = (Construction)Enum.ToObject(typeof(Construction), selectedTool);

        if (currentConstruction != _selectedConstruction)
        {
            _selectedConstruction = currentConstruction;
            RefreshCatalog();
        }

        if (_selectedConstruction != Construction.Ground)
            if (_previewObject != null)
                //DrawObjectSettings();
                EditionParametersPreview();

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

        if (_selectedConstruction == Construction.Ground)
        {
            // Блок превью для тайла
            if(CheckAllow(contactPoint,_tileLayerMask))
                if (CheckInput())
                    CreateObject(contactPoint);
        }
        else
        {
            //DrawPointer(contactPoint, Color.red);
            DrawPreview(contactPoint);
            
            if (CheckInput() && CheckAllow(contactPoint, _buildingsLayerMask))
            {
                CreateObject(contactPoint);
            }
                
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
        
        if (Physics.Raycast(guiRay, out RaycastHit raycastHit, Mathf.Infinity, _layerMasks[(int)_selectedConstruction]))
        {
            if(_selectedConstruction == Construction.Ground)    //Если тайллы, то по сетке.
            {
                int contactPointX = (int)Math.Round(raycastHit.point.x / _gridSize) * _gridSize; ;
                int contactPointZ = (int)Math.Round(raycastHit.point.z / _gridSize) * _gridSize; ;
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

    private void DrawPointer(Vector3 position, Color color)
    {
        Handles.color = color;
        Quaternion rotation = Quaternion.Euler(0, 0, 0);
        Mesh mesh = _catalog[_selectedElement].GetComponentsInChildren<MeshFilter>()[0].sharedMesh;
        Vector3 meshSize = mesh.bounds.size;
        Collider[] colliders = Physics.OverlapBox(position, meshSize, rotation, 6);
        Handles.DrawWireCube(position, meshSize);
    }
    
    private void DrawPreview(Vector3 contactPoint)
    {
        if (_previewObject == null)
            CreatePreviewObject();
        
        _previewObject.transform.position = contactPoint;        
        // _previewObject.transform.rotation = _previewRotation;
        // _previewObject.transform.localScale = _previewScale;
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
        _createdObject.layer = parent.gameObject.layer;

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
        Debug.Log(Event.current.keyCode);
        switch (Event.current.keyCode)
        {
            case KeyCode.V:
                _previewObject.transform.localScale +=  Vector3.one * 0.1f;
                break;
            case KeyCode.C:
                _previewObject.transform.localScale -=  Vector3.one * 0.1f;
                break;
            //Вращать влево
            case KeyCode.F:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.up, 20);
                break;
            //Вращать вправо
            case KeyCode.G:
                _previewObject.transform.RotateAround(_previewObject.transform.position, Vector3.down, 20);
                break;
        }
    }

    private void DrawCatalog(List<GUIContent> catalogIcons)
    {
        GUILayout.Label(_selectedConstruction.ToString());

        float targetSizeElement = 100f; // Примерный размер эллемента
        float minSizeElement = 50f; // Минимальный размер элемента
        float paddingLeft = 30f; // Отступ с лева, чтоб слайдер не загораживал
        float withWindow = position.width; // Ширина окна
        int column = Mathf.RoundToInt((withWindow - withWindow % targetSizeElement) / targetSizeElement); // Считает колличество столбцов
        column = Mathf.Clamp(column, 1, int.MaxValue); // Не дает колличеству столбцов равняться нулю
        float sizeElement = targetSizeElement + // К начальному размеру добавляет остаток
            (withWindow - column * targetSizeElement) / column // Определяет остаток не занятого пространства начального значения
            - paddingLeft / column; // Делит отступ на всех элементов
        sizeElement = Mathf.Clamp(sizeElement, minSizeElement, float.MaxValue); // Смотрит чтобы меньше минимального разменра не была
        int row = (catalogIcons.Count - catalogIcons.Count % column) / column; // Определаяте колличество строк
        
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
    

    private bool CheckAllow(Vector3 position, int layerMask)
    {
        Quaternion rotation = Quaternion.Euler(0, 0, 0);
        Mesh mesh = _catalog[_selectedElement].GetComponentsInChildren<MeshFilter>()[0].sharedMesh;
        Vector3 meshSize = mesh.bounds.size;
        Collider[] colliders = Physics.OverlapBox(position, meshSize/2, rotation, layerMask); //meshSize /2 тк в параметр идет половина размера 

        foreach (var collider in colliders)
        {
            Debug.Log(collider.gameObject.name);
        }
        return true;
    }
}