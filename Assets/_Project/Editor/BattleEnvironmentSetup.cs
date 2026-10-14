using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BattleEnvironmentSetup
{
    const string Root = "Assets/_Project/Environment/";
    static readonly string[] Props = { "P01_BroadleafTree", "P02_TallForestTree", "P03_Undergrowth", "P04_MossRocks", "P05_CaveEntrance", "P06_CaveWall", "P07_Stalagmites" };
    static Material[] propMaterials;
    static Material outdoorRockMaterial;
    static readonly Color OutdoorRockTint = new Color(.52f,.67f,.78f);
    // 배경만 차갑게 눌러 갈색 옷의 적과 구분한다. 적 재질·전역 조명에는 적용하지 않는다.
    static readonly Color[] PropTints = {
        new Color(.45f,.63f,.69f), new Color(.45f,.63f,.69f), new Color(.45f,.63f,.69f),
        new Color(.69f,.81f,.91f), new Color(.52f,.67f,.78f), new Color(.47f,.56f,.72f), new Color(.50f,.60f,.76f)
    };
    static readonly Color[] BackplateTints = {
        new Color(.86f,.96f,.86f), new Color(.57f,.74f,.83f), new Color(.62f,.77f,.86f), new Color(.49f,.58f,.73f)
    };

    [MenuItem("Tools/Scroll Hunter/Build Battle Backgrounds")]
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play first");
        if (GameObject.Find("BattleEnvironment")) throw new System.InvalidOperationException("Backgrounds already exist. Edit their transforms directly.");
        var camera = Camera.main;
        if (!camera) throw new System.InvalidOperationException("Main Camera missing");
        propMaterials = new Material[Props.Length];
        for (int i = 0; i < Props.Length; i++)
        {
            string path = Root + "Models/" + Props[i] + ".fbx";
            var model = (ModelImporter)AssetImporter.GetAtPath(path);
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.addCollider = false;
            model.SaveAndReimport();
            var normal = (TextureImporter)AssetImporter.GetAtPath(Root + "Textures/" + Props[i] + "_Normal.png");
            normal.textureType = TextureImporterType.NormalMap;
            normal.maxTextureSize = 1024;
            normal.SaveAndReimport();
            var material = Material(Props[i], "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", Texture(Props[i] + "_BaseColor"));
            material.SetTexture("_BumpMap", Texture(Props[i] + "_Normal"));
            material.SetColor("_BaseColor", PropTints[i]);
            material.SetFloat("_BumpScale", .35f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            propMaterials[i] = material;
        }
        // 바위는 보스 동굴에서도 쓰므로 일반 맵 재질만 따로 둔다.
        outdoorRockMaterial = Material("P04_MossRocks_Outdoor", "Universal Render Pipeline/Lit");
        outdoorRockMaterial.CopyPropertiesFromMaterial(propMaterials[3]);
        outdoorRockMaterial.SetColor("_BaseColor", OutdoorRockTint);
        var root = new GameObject("BattleEnvironment");
        Undo.RegisterCreatedObjectUndo(root, "Place battle backgrounds");
        var controller = root.AddComponent<BattleEnvironment>();
        string[] names = { "01_ForestEdge", "02_ForestTrail", "03_CaveEntrance", "04_CaveInterior" };
        string[] images = { "BG01_숲초입", "BG02_빽빽한숲", "BG03_동굴입구", "BG04_Cave_Backplate_Clean" };
        var stages = new GameObject[4]; var plates = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            stages[i] = new GameObject(names[i]); stages[i].transform.SetParent(root.transform, false);
            var mat = Material(names[i], "Universal Render Pipeline/Unlit");
            var image = (TextureImporter)AssetImporter.GetAtPath(Root + "Textures/" + images[i] + ".png");
            image.wrapMode = TextureWrapMode.Clamp; image.maxTextureSize = 2048; image.SaveAndReimport();
            mat.SetTexture("_BaseMap", Texture(images[i]));
            mat.SetColor("_BaseColor", BackplateTints[i]);
            plates[i] = Quad("Distant scenery", stages[i].transform, mat);
            plates[i].position = camera.transform.position + camera.transform.forward * 30f;
            plates[i].rotation = camera.transform.rotation;
        }
        var a = stages[0].transform;
        Prop(a, 0, -8, 5, 7.5f, 25); Prop(a, 0, 8.6f, 8, 8.5f, -35);
        Prop(a, 1, -6.8f, 14, 8, -10); Prop(a, 1, 7, 16, 9, 20);
        Prop(a, 3, -5.7f, 2, 1.2f, 20); Prop(a, 3, 6.2f, 4, 1.4f, -60);
        Prop(a, 2, -6.2f, 3.2f, .8f, 15); Prop(a, 2, 7, 5, .9f, 70);
        a = stages[1].transform;
        Prop(a, 0, -8.5f, 4, 9, 15); Prop(a, 0, 9, 5, 10, -30);
        Prop(a, 1, -10.5f, 6, 10, 40); Prop(a, 1, 10.5f, 8, 10, -40);
        Prop(a, 1, -12.5f, 13, 10, 75); Prop(a, 1, 12.5f, 15, 11, 10);
        Prop(a, 0, -13.5f, 16, 12, 20); Prop(a, 0, 13.5f, 17, 12, -50);
        Prop(a, 3, -6, 2, 1.1f, 25); Prop(a, 3, 6.6f, 3, 1.6f, -25);
        Prop(a, 2, -6.7f, 3, .85f, 90); Prop(a, 2, 6, 5, 1f, -20);
        a = stages[2].transform;
        Prop(a, 4, 0, 9, 9.7f, 0, 1.22f);
        Prop(a, 0, -10, 6, 10, 10); Prop(a, 1, 9.5f, 8, 11, -20);
        Prop(a, 3, -6.4f, 3, 1.8f, 30); Prop(a, 3, 6.8f, 4, 2, -30);
        Prop(a, 2, -6, 5, 1, 30); Prop(a, 2, 7, 6, 1.1f, -25);
        a = stages[3].transform;
        Prop(a, 5, -14, 10, 12, 55); Prop(a, 5, 14, 11, 12, -55);
        Prop(a, 6, -7, 3, 3, 20); Prop(a, 6, 7.5f, 4, 3.8f, -30);
        Prop(a, 6, -6.5f, 11, 2.2f, 80); Prop(a, 6, 6, 13, 2.6f, -65);
        Prop(a, 3, -6, 4, 1f, 40); Prop(a, 3, 6.5f, 6, 1.3f, -60);
        var circleMat = Material("BossWallCircle", "Universal Render Pipeline/Particles/Unlit");
        circleMat.SetTexture("_BaseMap", Texture("FX_BossWallCircle"));
        circleMat.SetFloat("_Surface", 1); circleMat.SetFloat("_Blend", 2);
        circleMat.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); circleMat.SetFloat("_DstBlend", (int)BlendMode.One);
        circleMat.SetFloat("_ZWrite", 0); circleMat.SetFloat("_Cull", 0);
        circleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        circleMat.SetOverrideTag("RenderType", "Transparent"); circleMat.renderQueue = 3000;
        var circle = Quad("Boss wall magic circle", a, circleMat);
        circle.position = new Vector3(0, 3.6f, 12);
        circle.rotation = camera.transform.rotation;
        circle.localScale = new Vector3(8f, 8f, 1);
        var so = new SerializedObject(controller);
        so.FindProperty("flow").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        so.FindProperty("view").objectReferenceValue = camera;
        foreach (var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.HasPhases) so.FindProperty("boss").objectReferenceValue = e;
        var items = so.FindProperty("stages"); items.arraySize = 4;
        var backgrounds = so.FindProperty("backplates"); backgrounds.arraySize = 4;
        for (int i = 0; i < 4; i++) { items.GetArrayElementAtIndex(i).objectReferenceValue = stages[i]; backgrounds.GetArrayElementAtIndex(i).objectReferenceValue = plates[i]; }
        so.FindProperty("magicCircle").objectReferenceValue = circle;
        so.ApplyModifiedPropertiesWithoutUndo();
        ApplyPhaseTransitionVfx();
        ApplyPhaseEnvironment();
        AddContactShadows();
        controller.Refresh(0);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Backgrounds placed: 4 stages / 7 VARCO props / wall circle. Scene not saved by builder.");
    }
    [MenuItem("Tools/Scroll Hunter/Apply Boss Phase Transition VFX")]
    public static void ApplyPhaseTransitionVfx()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play first");
        var controller = UnityEngine.Object.FindFirstObjectByType<BattleEnvironment>();
        var so = new SerializedObject(controller);
        var circle = (Transform)so.FindProperty("magicCircle").objectReferenceValue;
        var material = Material("BossPhaseWave", "Universal Render Pipeline/Particles/Unlit");
        material.CopyPropertiesFromMaterial(circle.GetComponent<Renderer>().sharedMaterial);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/UI/SanctuaryVfx/lightmask_ring_b.png"));
        material.SetColor("_BaseColor", new Color(1.15f, .3f, 1.8f, 0f));
        EditorUtility.SetDirty(material);
        var wave = circle.parent.Find("Boss phase wave");
        if (!wave) wave = Quad("Boss phase wave", circle.parent, material);
        wave.position = circle.position - circle.forward * .03f;
        wave.rotation = circle.rotation;
        wave.localScale = Vector3.one;
        var renderer = wave.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.enabled = false;
        so.FindProperty("phaseWave").objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
    }


    [MenuItem("Tools/Scroll Hunter/Apply Boss Phase Environment")]
    public static void ApplyPhaseEnvironment()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play first");
        var controller = UnityEngine.Object.FindFirstObjectByType<BattleEnvironment>();
        var so = new SerializedObject(controller);
        var circle = (Transform)so.FindProperty("magicCircle").objectReferenceValue;
        var root = circle.parent.Find("Phase surroundings");
        if (!root) { root = new GameObject("Phase surroundings").transform; root.SetParent(circle.parent, false); }
        var glowMat = Material("BossPhaseGlow", "Universal Render Pipeline/Particles/Unlit");
        glowMat.CopyPropertiesFromMaterial(circle.GetComponent<Renderer>().sharedMaterial);
        glowMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/VFX/Textures/smoke_04.png"));
        glowMat.SetColor("_BaseColor", new Color(.75f,.17f,1.3f,0));
        EditorUtility.SetDirty(glowMat);
        var glows = new Renderer[4];
        for (int i=0;i<glows.Length;i++)
        {
            string name = i==2 ? "Floor violet glow" : i==3 ? "Background violet fog" : "Wall violet glow " + i;
            var q = root.Find(name); if(!q)q=Quad(name,root,glowMat);
            q.GetComponent<Renderer>().sharedMaterial=glowMat;
            q.position = i==2 ? new Vector3(0,-.10f,6) : i==3 ? new Vector3(0,4f,11.75f) : new Vector3(i==0?-4.7f:4.7f,2.8f,11.8f);
            q.rotation = i==2 ? Quaternion.Euler(90,0,0) : Camera.main.transform.rotation;
            q.localScale = i==2 ? new Vector3(20,15,1) : i==3 ? new Vector3(20,12,1) : new Vector3(12,10,1);
            glows[i]=q.GetComponent<Renderer>(); glows[i].enabled=false;
        }
        var currentMat=Material("BossPhaseCracks","Universal Render Pipeline/Particles/Unlit");
        currentMat.CopyPropertiesFromMaterial(glowMat);currentMat.SetTexture("_BaseMap",Texture2D.whiteTexture);
        EditorUtility.SetDirty(currentMat);
        foreach(string prefix in new[]{"Wall crack ","Wall branch ","Floor crack ","Floor branch "})
            foreach(int side in new[]{-1,1}) {var old=root.Find(prefix+side);if(old)Object.DestroyImmediate(old.gameObject);}
        var cracks=new System.Collections.Generic.List<Renderer>();
        float radius=so.FindProperty("circleDiameters").GetArrayElementAtIndex(2).floatValue*.42f;
        float[] angles={155f,25f,210f,330f};
        Vector3[][] routes={
            new[]{new Vector3(-5.8f,5.3f,11.7f),new Vector3(-6.5f,4.75f,11.7f),new Vector3(-7.2f,5.2f,11.7f),new Vector3(-7.8f,4.7f,11.7f)},
            new[]{new Vector3(5.9f,5.6f,11.7f),new Vector3(6.35f,4.8f,11.7f),new Vector3(7.25f,4.4f,11.7f),new Vector3(7.5f,3.7f,11.7f)},
            new[]{new Vector3(-5.15f,.25f,10f),new Vector3(-5.0f,-.10f,7.6f),new Vector3(-4.3f,-.10f,5.8f),new Vector3(-3.6f,-.10f,3.2f)},
            new[]{new Vector3(5.4f,.3f,10f),new Vector3(5.55f,-.10f,7.2f),new Vector3(4.5f,-.10f,5.4f),new Vector3(4.05f,-.10f,2.6f)}
        };
        for(int i=0;i<routes.Length;i++)
        {
            float angle=angles[i]*Mathf.Deg2Rad;
            var start=circle.position+Camera.main.transform.right*(Mathf.Cos(angle)*radius)
                +Camera.main.transform.up*(Mathf.Sin(angle)*radius)-Camera.main.transform.forward*.06f;
            var anchors=new System.Collections.Generic.List<Vector3>{start};anchors.AddRange(routes[i]);
            var points=new System.Collections.Generic.List<Vector3>();
            for(int j=0;j<anchors.Count-1;j++)
            {
                int steps=Mathf.Max(3,Mathf.CeilToInt(Vector3.Distance(anchors[j],anchors[j+1])/.22f));
                for(int k=0;k<steps;k++)points.Add(Vector3.Lerp(anchors[j],anchors[j+1],(float)k/steps));
            }
            points.Add(anchors[anchors.Count-1]);
            cracks.Add(PhaseLine(root,"Violet current "+i,currentMat,points.ToArray(),.024f));
            cracks.Add(PhaseLine(root,"Violet current glow "+i,currentMat,points.ToArray(),.12f));
        }
        var emberRoot=root.Find("Rising violet motes");
        if(!emberRoot){emberRoot=new GameObject("Rising violet motes").transform;emberRoot.SetParent(root,false);}
        emberRoot.localPosition=new Vector3(0,2.3f,9.5f);
        var ps=emberRoot.GetComponent<ParticleSystem>();if(!ps)ps=emberRoot.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed=false;ps.randomSeed=214;
        var main=ps.main; main.playOnAwake=false;main.loop=true;main.useUnscaledTime=false;main.maxParticles=40;
        main.startLifetime=new ParticleSystem.MinMaxCurve(3f,5f);main.startSize=new ParticleSystem.MinMaxCurve(.05f,.12f);
        main.startSpeed=0;main.startColor=new Color(.7f,.28f,1f,.55f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var emission=ps.emission;emission.rateOverTime=6;
        var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(12,3.2f,1);
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.y=.35f;velocity.x=.04f;
        var colors=ps.colorOverLifetime;colors.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(.7f,.7f),new GradientAlphaKey(0,1)});
        colors.color=gradient;
        var emberMat=Material("BossPhaseMotes","Universal Render Pipeline/Particles/Unlit");
        emberMat.CopyPropertiesFromMaterial(glowMat);
        emberMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/VFX/Textures/circle_05.png"));
        emberMat.SetColor("_BaseColor",new Color(1.2f,.7f,2f,1));EditorUtility.SetDirty(emberMat);
        var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=emberMat;pr.shadowCastingMode=ShadowCastingMode.Off;pr.receiveShadows=false;
        var array=so.FindProperty("phaseGlows");array.arraySize=glows.Length;
        for(int i=0;i<glows.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=glows[i];
        array=so.FindProperty("phaseCracks");array.arraySize=cracks.Count;
        for(int i=0;i<cracks.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=cracks[i];
        so.FindProperty("phaseEmbers").objectReferenceValue=ps;so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    static LineRenderer PhaseLine(Transform root,string name,Material material,Vector3[] points,float width)
    {
        var child=root.Find(name);
        if(!child){child=new GameObject(name).transform;child.SetParent(root,false);}
        var line=child.GetComponent<LineRenderer>();if(!line)line=child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);
        line.widthMultiplier=width;line.widthCurve=new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.15f,1f),new Keyframe(.85f,.7f),new Keyframe(1,0));
        line.numCornerVertices=0;line.numCapVertices=0;line.alignment=LineAlignment.View;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
        return line;
    }

    public static void AddContactShadows()
    {
        var mat = Material("EnemyContactShadow", "Universal Render Pipeline/Unlit");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/VFX/Textures/circle_05.png"));
        mat.SetColor("_BaseColor", new Color(0, 0, 0, .45f));
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent"); mat.renderQueue = 2990;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemy.IsBossOrb || enemy.transform.Find("Environment contact shadow")) continue;
            var shadow = Quad("Environment contact shadow", enemy.transform, mat);
            shadow.position = new Vector3(enemy.transform.position.x, -.12f, enemy.transform.position.z);
            shadow.rotation = Quaternion.Euler(90, 0, 0);
            shadow.localScale = new Vector3(1.7f, 1.1f, 1f);
        }
    }
    static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + name + ".png");
    static Material Material(string name, string shader)
    {
        var path = Root + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
        return material;
    }
    static Transform Quad(string name, Transform parent, Material material)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Quad); o.name = name; o.transform.SetParent(parent, false);
        UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
        var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return o.transform;
    }
    static void Prop(Transform parent, int index, float x, float z, float height, float yaw, float width = 1f)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/" + Props[index] + ".fbx");
        var o = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        o.name = Props[index]; o.transform.localRotation = Quaternion.Euler(0, yaw, 0) * model.transform.localRotation;
        var renderers = o.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.sharedMaterial = index == 3 && parent.name != "04_CaveInterior" ? outdoorRockMaterial : propMaterials[index]; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        o.transform.localScale *= height / bounds.size.y;
        bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        o.transform.position += new Vector3(x - bounds.center.x, -.18f - Mathf.Max(0f, z - 3f) * .18f - bounds.min.y, z - bounds.center.z);
        o.transform.localScale = Vector3.Scale(o.transform.localScale, new Vector3(width, 1f, 1f));
    }
}
