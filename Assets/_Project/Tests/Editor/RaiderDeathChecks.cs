using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// 약탈자 사망 전용 클립의 원본 보존·다리 반전·마지막 자세를 실제 Humanoid로 비교한다.
public static class RaiderDeathChecks
{
    static readonly HumanBodyBones[] Bones = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes };
    static void Sample(AnimationClip clip, out Vector3[,] positions, out Quaternion[,] rotations)
    {
        int frames = Mathf.RoundToInt(clip.length * 60f) + 1;
        positions = new Vector3[frames,Bones.Length]; rotations = new Quaternion[frames,Bones.Length];
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var graph = PlayableGraph.Create("Raider death check");
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/ENM_Raider_GRN_v01.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); go.transform.localScale = Vector3.one;
            var animator = go.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph,clip); playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(playable); graph.Play();
            for (int frame=0;frame<frames;frame++)
            {
                playable.SetTime(frame/60f); graph.Evaluate(0f);
                for (int bone=0;bone<Bones.Length;bone++)
                { var t=animator.GetBoneTransform(Bones[bone]); positions[frame,bone]=t.position; rotations[frame,bone]=t.localRotation; }
            }
        }
        finally { graph.Destroy(); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
    [MenuItem("Tools/Scroll Hunter/Check Raider Death Stabilization")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        var source=EnemyRigBuilder.Clip("MX_SwordShieldDeath");
        var corrected=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Raider/Raider_DeathStable.anim");
        int checks=0; Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
        check(corrected!=null && corrected.isHumanMotion && !corrected.isLooping && Mathf.Abs(corrected.length-source.length)<.0001f,"humanoid/duration/loop");
        var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animations/Raider/ENM_Raider_GRN_v01.controller");
        check(controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Death").state.motion==corrected,"controller connection");
        Sample(source,out var beforePosition,out var beforeRotation); Sample(corrected,out var afterPosition,out var afterRotation);
        float earlyPosition=0f,earlyAngle=0f,beforePeak=0f,afterPeak=0f,settledAngle=0f,settledPosition=0f;
        for(int frame=0;frame<afterPosition.GetLength(0);frame++)
            for(int bone=0;bone<Bones.Length;bone++)
            {
                if(frame<=195){earlyPosition=Mathf.Max(earlyPosition,Vector3.Distance(beforePosition[frame,bone],afterPosition[frame,bone]));earlyAngle=Mathf.Max(earlyAngle,Quaternion.Angle(beforeRotation[frame,bone],afterRotation[frame,bone]));}
                if(frame>210){beforePeak=Mathf.Max(beforePeak,Quaternion.Angle(beforeRotation[frame-1,bone],beforeRotation[frame,bone]));afterPeak=Mathf.Max(afterPeak,Quaternion.Angle(afterRotation[frame-1,bone],afterRotation[frame,bone]));}
                if(frame>220){settledAngle=Mathf.Max(settledAngle,Quaternion.Angle(afterRotation[frame-1,bone],afterRotation[frame,bone]));settledPosition=Mathf.Max(settledPosition,Vector3.Distance(afterPosition[frame-1,bone],afterPosition[frame,bone]));}
            }
        check(earlyPosition<.001f && earlyAngle<.2f,"original fall through 3.25s");
        check(afterPeak<5f && afterPeak<beforePeak*.1f,"late leg rotation stabilized");
        check(settledAngle<.05f && settledPosition<.00001f,"final pose holds");
        return $"Raider death: {checks} passed; 235 samples per clip at 60Hz. Early delta {earlyPosition:F6}m/{earlyAngle:F3}deg; last 0.4s max bone rotation {beforePeak:F3} -> {afterPeak:F3}deg/frame; settled {settledPosition:F7}m/{settledAngle:F4}deg. Preview-scene Humanoid samples, not player input.";
    }
}
