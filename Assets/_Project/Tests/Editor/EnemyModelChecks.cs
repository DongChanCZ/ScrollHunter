using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 모델 Unity 반입 검사(10/2, 09 적 모델 Unity 확인). 근거자료 원본과 Unity 파일의 해시·FBX 내장 텍스처,
/// Humanoid 아바타·머티리얼 연결·크기·방향을 확인한다. 씬·에셋을 바꾸지 않는다.
/// Capture는 미리보기 장면에만 모델을 만들고 근육 값으로 시험 자세를 잡는다. 애니메이션 품질 검사가 아니다.
/// </summary>
public static class EnemyModelChecks
{
    private const string UnityRoot = "Assets/_Project/Models/Enemies/";

    // 화면 순서: 튜토리얼·A·B·C·보스. 별도 보정 PNG가 없는 약탈자는 원본 FBX 내장 텍스처와만 대조한다.
    private static readonly (string unity, string source, bool correctedPng, string pngSource)[] Models =
    {
        ("Thug/ENM_Thug_ALL_v01", "2026-10-02_적모델링/07_깡패_보정_리깅", true, null),
        ("Raider/ENM_Raider_GRN_v01", "2026-10-01_적모델링/02_약탈자_손가락리깅", false, null),
        ("Archer/ENM_Archer_ORG_v01", "2026-10-02_적모델링/03_궁병_손보정_리깅", true, null),
        ("Leader/ENM_Leader_RED_v01", "2026-10-07_튜토리얼사망_우두머리얼굴/Leader_face_weights", true, "2026-10-02_적모델링/05_우두머리_손보정_리깅"),
        // 10/6 손가락 가중치 작업본(원본 09 blend 보존). 텍스처는 09와 같다.
        ("Mage/BOSS_Mage_ALL_v01", "2026-10-06_적모델보정_전투연결/10_마법사_손가락가중치", true, "2026-10-02_적모델링/09_마법사_보정_리깅"),
    };

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [MenuItem("Tools/Scroll Hunter/Check Enemy Models")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder();
        int passed = 0;
        var failed = new List<string>();
        void Check(bool ok, string label)
        {
            if (ok) passed++; else failed.Add(label);
        }

        string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "근거자료"));
        foreach (var m in Models)
        {
            string unityBase = UnityRoot + m.unity;
            string sourceBase = Path.Combine(evidence, m.source);
            string fbx = unityBase + ".fbx";
            byte[] sourceFbx = File.ReadAllBytes(sourceBase + ".fbx");
            Check(Hash(File.ReadAllBytes(fbx)) == Hash(sourceFbx), m.unity + " FBX = 근거자료 원본");

            List<string> embedded = EmbeddedPngs(sourceFbx).Select(Hash).ToList();
            foreach (string label in new[] { "BaseColor", "Normal" })
            {
                string unityHash = Hash(File.ReadAllBytes($"{unityBase}_{label}.png"));
                Check(embedded.Contains(unityHash), $"{m.unity} {label} = 원본 FBX 내장 텍스처");
                if (m.correctedPng)
                    Check(unityHash == Hash(File.ReadAllBytes($"{(m.pngSource == null ? sourceBase : Path.Combine(evidence, m.pngSource))}_{label}.png")), $"{m.unity} {label} = 보정 PNG");
            }

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbx);
            Avatar avatar = subAssets.OfType<Avatar>().FirstOrDefault();
            Check(importer.animationType == ModelImporterAnimationType.Human && avatar != null && avatar.isValid && avatar.isHuman,
                m.unity + " Humanoid 아바타 유효");
            var map = importer.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            Check(Enumerable.Range(0, HumanTrait.BoneCount).Where(HumanTrait.RequiredBone).All(i => map.ContainsKey(HumanTrait.BoneName[i])),
                m.unity + " 필수 뼈 15개 매핑");
            Check(HumanTrait.BoneName.Count(n => IsFinger(n) && map.ContainsKey(n)) == 30, m.unity + " 손가락 뼈 30개 매핑");
            Check(!subAssets.OfType<AnimationClip>().Any(c => !c.name.StartsWith("__preview__")), m.unity + " 애니메이션 클립 없음");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(unityBase + ".mat");
            Check(renderers.Length == 1 && renderers[0].sharedMaterials.Length == 1 && renderers[0].sharedMaterial == material,
                m.unity + " 렌더러 1개·전용 머티리얼");
            Check(material != null && material.shader.name == "Universal Render Pipeline/Lit"
                && material.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(unityBase + "_BaseColor.png")
                && material.GetTexture("_BumpMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(unityBase + "_Normal.png")
                && material.IsKeywordEnabled("_NORMALMAP"), m.unity + " URP Lit·BaseColor·Normal 연결");
            var baseImporter = (TextureImporter)AssetImporter.GetAtPath(unityBase + "_BaseColor.png");
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(unityBase + "_Normal.png");
            Check(baseImporter.sRGBTexture && normalImporter.textureType == TextureImporterType.NormalMap,
                m.unity + " BaseColor sRGB·Normal 노멀맵 형식");

            Transform Bone(string human) => model.GetComponentsInChildren<Transform>().First(t => t.name == map[human]);
            Vector3 toe = Bone("LeftToes").position - Bone("LeftFoot").position;
            float head = Bone("Head").position.y;
            float foot = Mathf.Min(Bone("LeftFoot").position.y, Bone("RightFoot").position.y);
            Check(toe.z < 0f, m.unity + " 발끝 -Z(전투 카메라 쪽)");
            Check(foot >= 0f && foot < 0.15f && head > 0.6f && head < 1.0f, m.unity + " 발바닥 원점·키 약 1m");
            log.AppendLine($"  {m.unity}: 뼈 매핑 {map.Count}개, 머리 뼈 높이 {head:0.000}, 손 축척 {Bone("LeftHand").lossyScale.x:0.##}");
        }

        log.Insert(0, $"[적 모델 검사] 통과 {passed} / 실패 {failed.Count}\n");
        foreach (string f in failed) log.AppendLine("  실패: " + f);
        if (failed.Count > 0) throw new Exception(log.ToString());
        return log.ToString();
    }

    /// <summary>
    /// 5종을 튜토리얼·A·B·C·보스 순서로 나란히 그린다(only ≥ 0이면 그 모델만 원점에).
    /// pose: null=원래 T자, "down"=팔 내림·주먹, "bend"=팔꿈치 약 80°·왼무릎 들기. yaw 0=정면, 90=오른쪽 옆, 180=뒤.
    /// pitch 음수는 아래에서 올려다본다(T자 손바닥 확인).
    /// </summary>
    public static string Capture(string path, string pose, float yaw, float focusX = 0f, float focusY = 0.48f,
        float viewHeight = 1.95f, int width = 2000, int height = 650, int only = -1, float pitch = 0f)
    {
        // 첫 캡처에서 비동기 셰이더 컴파일 중인 모델이 빠지지 않게 캡처 동안만 동기 컴파일.
        bool asyncShaders = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var preview = new PreviewRenderUtility();
        try
        {
            Camera cam = preview.camera;
            preview.cameraFieldOfView = cam.fieldOfView = 12f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.31f, 0.33f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30f, yaw + 30f, 0f);
            preview.lights[1].intensity = 0.5f;
            preview.lights[1].transform.rotation = Quaternion.Euler(20f, yaw + 200f, 0f);
            preview.ambientColor = new Color(0.32f, 0.32f, 0.35f);

            Quaternion view = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = view * Vector3.right;
            for (int i = 0; i < Models.Length; i++)
            {
                if (only >= 0 && i != only) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(UnityRoot + Models[i].unity + ".fbx");
                GameObject go = preview.InstantiatePrefabInScene(asset);
                // 자세를 원점에서 잡은 뒤 옮긴다. 옮긴 뒤 잡으면 SetHumanPose가 몸 위치를 한 번 더 더한다.
                ApplyPose(go, pose);
                go.transform.position = only >= 0 ? Vector3.zero : right * ((i - 2) * 1.15f);
            }

            Vector3 target = right * focusX + Vector3.up * focusY;
            float distance = viewHeight * 0.5f / Mathf.Tan(6f * Mathf.Deg2Rad);
            Quaternion look = view * Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.rotation = look;
            cam.transform.position = target - look * Vector3.forward * distance;
            preview.BeginStaticPreview(new Rect(0, 0, width, height));
            preview.Render(true, false);
            Texture2D image = preview.EndStaticPreview();
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            return full;
        }
        finally
        {
            preview.Cleanup();
            ShaderUtil.allowAsyncCompilation = asyncShaders;
        }
    }

    private static void ApplyPose(GameObject go, string pose)
    {
        if (string.IsNullOrEmpty(pose)) return;
        var handler = new HumanPoseHandler(go.GetComponent<Animator>().avatar, go.transform);
        var human = new HumanPose();
        handler.GetHumanPose(ref human);
        for (int i = 0; i < human.muscles.Length; i++)
        {
            string n = HumanTrait.MuscleName[i];
            if (pose == "down")
            {
                if (n.EndsWith("Arm Down-Up")) human.muscles[i] = -0.9f;
                if (IsFinger(n) && n.Contains("Stretched")) human.muscles[i] = -1f;
            }
            else if (pose == "bend")
            {
                // T자에서 다리 Front-Back 약 0.6(음수가 앞), 무릎·팔꿈치 Stretch 1(0이면 약 80° 굽힘).
                if (n.EndsWith("Arm Down-Up")) human.muscles[i] = -0.4f;
                if (n.EndsWith("Arm Front-Back")) human.muscles[i] = 0.6f;
                if (n.EndsWith("Forearm Stretch")) human.muscles[i] = 0f;
                if (n == "Left Upper Leg Front-Back") human.muscles[i] = -0.3f;
                if (n == "Left Lower Leg Stretch") human.muscles[i] = 0f;
            }
        }
        handler.SetHumanPose(ref human);
        handler.Dispose();
    }

    private static bool IsFinger(string name) =>
        name.Contains("Thumb") || name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little");

    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>바이너리 FBX의 내장 PNG를 서명부터 IEND 청크까지 잘라낸다.</summary>
    private static IEnumerable<byte[]> EmbeddedPngs(byte[] bytes)
    {
        for (int i = 0; i + PngSignature.Length < bytes.Length; i++)
        {
            int k = 0;
            while (k < PngSignature.Length && bytes[i + k] == PngSignature[k]) k++;
            if (k < PngSignature.Length) continue;
            int p = i + PngSignature.Length;
            while (p + 8 <= bytes.Length)
            {
                int length = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
                string type = Encoding.ASCII.GetString(bytes, p + 4, 4);
                p += 12 + length;
                if (type != "IEND") continue;
                var png = new byte[p - i];
                Array.Copy(bytes, i, png, 0, png.Length);
                yield return png;
                i = p - 1;
                break;
            }
        }
    }
}
