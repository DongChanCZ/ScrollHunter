using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 동작 비교 화면(10/6, 09 적 모델 Unity 확인). Play 중 프리팹을 씬 밖(x 200 부근)에 만들고
/// 실제 Animator 자세로 찍는다. 미리보기 SampleAnimation은 회전 보정을 반영하지 않아 쓰지 않는다.
/// 사용: Spawn으로 만든 뒤 한 프레임 진행하고(Animator 초기화) Sheet·GameSheet를 부른다. 씬은 저장하지 않는다.
/// </summary>
public static class EnemyAnimCapture
{
    public static readonly Vector3 Stage = new Vector3(200f, 0f, 0f);

    public static GameObject Spawn(string prefabPath, Vector3 offset)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        // 같은 이름을 다시 만들면 이전 개체를 지운다(MCP 호출이 재시도돼 겹쳐 그려지는 일을 막음).
        foreach (var old in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (old.name == "CAL_" + prefab.name) Object.DestroyImmediate(old.gameObject);
        var go = Object.Instantiate(prefab, Stage + offset, Quaternion.identity);
        go.name = "CAL_" + prefab.name;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.forceMatrixRecalculationPerRender = true;   // 한 번의 실행 안에서 자세를 바꿔 찍을 때 스킨 갱신
            r.updateWhenOffscreen = true;
        }
        return go;
    }

    /// <summary>상태 목록("Idle:0.5")을 순서대로 적용해 가로로 이어 붙인다. cameraOffset은 대상 기준.</summary>
    public static string Sheet(GameObject go, string states, string outPath, int w, int h,
        Vector3 cameraOffset, Vector3 lookOffset, float fov = 26f, string bone = null)
    {
        var animator = go.GetComponent<Animator>();
        var list = Parse(animator, states);
        var sheet = new Texture2D(w * list.Count, h, TextureFormat.RGB24, false);
        WithCamera(w, h, fov, (cam, rt) =>
        {
            for (int i = 0; i < list.Count; i++)
            {
                Pose(animator, list[i]);
                Vector3 focus = bone == null ? go.transform.position
                    : animator.GetBoneTransform((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), bone)).position;
                cam.transform.position = focus + cameraOffset;
                cam.transform.LookAt(focus + lookOffset);
                Grab(cam, rt, sheet, i * w, 0);
            }
        });
        return Save(sheet, outPath, list);
    }

    /// <summary>
    /// 전투 카메라와 같은 화각·상대 위치로 1920×1080을 그린 뒤 대상 주변을 같은 크기로 잘라 이어 붙인다.
    /// 대상은 전투 1번 자리 중앙(x 0)에 서 있는 것처럼 둔다(Stage 기준).
    /// </summary>
    public static string GameSheet(GameObject go, string states, string outPath, int cropW = 560, int cropH = 620)
    {
        var animator = go.GetComponent<Animator>();
        var list = Parse(animator, states);
        var main = Camera.main;
        var sheet = new Texture2D(cropW * list.Count, cropH, TextureFormat.RGB24, false);
        WithCamera(1920, 1080, main.fieldOfView, (cam, rt) =>
        {
            cam.CopyFrom(main);
            cam.enabled = false;
            cam.targetTexture = rt;
            // 전투 카메라를 Stage만큼 옮긴다. Stage+(x,0,0)의 대상은 전투의 x 자리에 선 것과 같게 보인다.
            cam.transform.SetPositionAndRotation(main.transform.position + Stage, main.transform.rotation);
            Vector3 screen = cam.WorldToScreenPoint(go.transform.position + Vector3.up * 1.0f);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(screen.x - cropW / 2f), 0, 1920 - cropW);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(screen.y - cropH / 2f), 0, 1080 - cropH);
            for (int i = 0; i < list.Count; i++)
            {
                Pose(animator, list[i]);
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(x0, y0, cropW, cropH), i * cropW, 0);
            }
        });
        return Save(sheet, outPath, list);
    }

    private static List<(int hash, float t, string label)> Parse(Animator animator, string states)
    {
        var list = new List<(int, float, string)>();
        foreach (string s in states.Split(','))
        {
            string[] p = s.Trim().Split(':');
            int hash = Animator.StringToHash(p[0]);
            if (!animator.HasState(0, hash)) continue;
            list.Add((hash, p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 0.5f, s.Trim()));
        }
        return list;
    }

    private static void Pose(Animator animator, (int hash, float t, string label) state)
    {
        foreach (var p in animator.parameters)
            if (p.name == "AttackTime") animator.SetFloat(p.nameHash, state.t);
        animator.Play(state.hash, 0, state.t);
        animator.Update(0f);
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        foreach (var b in animator.GetComponents<MonoBehaviour>())
        {
            if (!b.enabled) continue;
            b.GetType().GetMethod("Snap", Any)?.Invoke(b, null);   // 상태별 가중치를 바로 맞춤(HandIK)
            b.GetType().GetMethod("LateUpdate", Any)?.Invoke(b, null);   // 손 IK 같은 후처리 반영
        }
    }

    private static void WithCamera(int w, int h, float fov, System.Action<Camera, RenderTexture> draw)
    {
        ShaderUtil.allowAsyncCompilation = false;
        var go = new GameObject("CAL_Cam");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.32f, 0.34f, 0.38f);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.enabled = false;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        try { draw(cam, rt); }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(go);
        }
    }

    private static void Grab(Camera cam, RenderTexture rt, Texture2D sheet, int x, int y)
    {
        cam.Render();
        RenderTexture.active = rt;
        sheet.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y);
    }

    private static string Save(Texture2D sheet, string outPath, List<(int hash, float t, string label)> list)
    {
        sheet.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        var labels = new List<string>();
        foreach (var s in list) labels.Add(s.label);
        return outPath + " [" + string.Join(", ", labels) + "]";
    }
}
