using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FWF.Destruction;
using FWF.Input;
using FWF.Player;

/// <summary>
/// Generates a playable graybox test scene from code, so no manual scene setup is needed.
/// In the editor: menu FWF > Build Test Scene. In CI: called by BuildScript.
/// </summary>
public static class SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    const string DebrisPath = "Assets/Prefabs/Debris.prefab";

    [MenuItem("FWF/Build Test Scene")]
    public static void Create()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Prefabs");
        Directory.CreateDirectory("Assets/Materials");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Sun
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.GetComponent<Renderer>().sharedMaterial = Mat("Grass", new Color(0.35f, 0.55f, 0.3f));

        var concrete = Mat("Concrete", new Color(0.65f, 0.65f, 0.68f));
        var cracked = Mat("Cracked", new Color(0.45f, 0.45f, 0.48f));

        // Debris prefab + pool
        var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debris.name = "Debris";
        debris.transform.localScale = Vector3.one * 0.3f;
        debris.GetComponent<Renderer>().sharedMaterial = cracked;
        var rb = debris.AddComponent<Rigidbody>();
        var chunk = debris.AddComponent<DebrisChunk>();
        var cso = new SerializedObject(chunk);
        cso.FindProperty("rb").objectReferenceValue = rb;
        cso.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(debris, DebrisPath);
        Object.DestroyImmediate(debris);

        var pool = new GameObject("DebrisPool").AddComponent<DebrisPool>();
        var pso = new SerializedObject(pool);
        var entries = pso.FindProperty("entries");
        entries.arraySize = 1;
        var e0 = entries.GetArrayElementAtIndex(0);
        e0.FindPropertyRelative("id").stringValue = "debris_concrete";
        e0.FindPropertyRelative("prefab").objectReferenceValue = prefab.GetComponent<DebrisChunk>();
        e0.FindPropertyRelative("prewarm").intValue = 30;
        pso.ApplyModifiedPropertiesWithoutUndo();

        // Destructible walls (low health so you can test by shooting)
        MakeWall(new Vector3(0, 0, 8),   new Vector3(6, 2.5f, 0.6f), concrete, cracked);
        MakeWall(new Vector3(-8, 0, 12), new Vector3(0.6f, 2.5f, 6), concrete, cracked);
        MakeWall(new Vector3(8, 0, 12),  new Vector3(0.6f, 2.5f, 6), concrete, cracked);
        MakeWall(new Vector3(0, 0, 18),  new Vector3(10, 2.5f, 0.6f), concrete, cracked);

        // Player
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0.1f, 0);
        var cc = player.AddComponent<CharacterController>();
        cc.radius = 0.4f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0, 0.9f, 0);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = Mat("Player", new Color(0.9f, 0.4f, 0.1f));

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0, 1.5f, 0);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.transform.SetParent(pivot, false);
        camGo.transform.localPosition = new Vector3(0.6f, 0.3f, -4f);

        var ctrl = player.AddComponent<ThirdPersonController>();
        var tso = new SerializedObject(ctrl);
        tso.FindProperty("cameraPivot").objectReferenceValue = pivot;
        tso.ApplyModifiedPropertiesWithoutUndo();

        BuildUI();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    // ---------- helpers ----------
    static Material Mat(string name, Color c)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Standard")) { color = c };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject VisualCube(string name, Transform parent, Vector3 size, Material mat)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        Object.DestroyImmediate(c.GetComponent<Collider>());
        c.transform.SetParent(parent, false);
        c.transform.localScale = size;
        c.GetComponent<Renderer>().sharedMaterial = mat;
        return c;
    }

    static void MakeWall(Vector3 pos, Vector3 size, Material intactMat, Material damagedMat)
    {
        var root = new GameObject("Wall");
        root.transform.position = pos + Vector3.up * size.y * 0.5f;
        var col = root.AddComponent<BoxCollider>();   // collider on root so it survives visual swaps
        col.size = size;

        var intact = VisualCube("Intact", root.transform, size, intactMat);
        var dmg = VisualCube("Damaged", root.transform, new Vector3(size.x, size.y * 0.6f, size.z), damagedMat);
        dmg.transform.localPosition = new Vector3(0, -size.y * 0.2f, 0);
        dmg.SetActive(false);

        var dc = root.AddComponent<DestructibleCover>();
        var so = new SerializedObject(dc);
        so.FindProperty("intactVisual").objectReferenceValue = intact;
        so.FindProperty("damagedVisual").objectReferenceValue = dmg;
        so.FindProperty("fullCollider").objectReferenceValue = col;
        so.FindProperty("maxHealth").floatValue = 100f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildUI()
    {
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileInputHub));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Left half: joystick
        var joyArea = Zone("JoystickArea", canvasGo.transform, new Vector2(0, 0), new Vector2(0.5f, 1));
        var joyBg = Panel("Background", joyArea, new Vector2(-450, -200), 220, new Color(1, 1, 1, 0.25f));
        var joyHandle = Panel("Handle", joyBg, Vector2.zero, 100, new Color(1, 1, 1, 0.6f));
        var joy = joyArea.gameObject.AddComponent<VirtualJoystick>();
        var jso = new SerializedObject(joy);
        jso.FindProperty("background").objectReferenceValue = joyBg;
        jso.FindProperty("handle").objectReferenceValue = joyHandle;
        jso.ApplyModifiedPropertiesWithoutUndo();

        // Right half: look pad
        var lookArea = Zone("LookArea", canvasGo.transform, new Vector2(0.5f, 0), new Vector2(1, 1));
        lookArea.gameObject.AddComponent<LookPad>();

        // Buttons (created after the look pad so they sit on top)
        Button(canvasGo.transform, "FIRE",   PressButton.Kind.Fire,   new Vector2(-200, 230), 220);
        Button(canvasGo.transform, "JUMP",   PressButton.Kind.Jump,   new Vector2(-130, 480), 150);
        Button(canvasGo.transform, "CROUCH", PressButton.Kind.Crouch, new Vector2(-440, 120), 150);
        Button(canvasGo.transform, "AIM",    PressButton.Kind.Aim,    new Vector2(-440, 330), 150);
    }

    static RectTransform Zone(string name, Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible but receives touches
        return rt;
    }

    static RectTransform Panel(string name, RectTransform parent, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
        return rt;
    }

    static void Button(Transform parent, string label, PressButton.Kind kind, Vector2 pos, float size)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(PressButton));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0);     // bottom-right
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.45f);
        go.GetComponent<PressButton>().kind = kind;

        var tgo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)tgo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = tgo.GetComponent<Text>();
        t.text = label; t.alignment = TextAnchor.MiddleCenter; t.color = Color.black;
        t.fontSize = 34; t.raycastTarget = false;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
