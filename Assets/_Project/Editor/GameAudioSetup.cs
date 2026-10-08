using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임 소리 연결(10/8, 10 A48). 메뉴 Tools > Scroll Hunter > Apply Game Audio.
/// Audio 폴더 반입 설정을 맞추고 GameSoundLibrary를 기본 배정으로 채운 뒤 Battle 씬 GameAudio에 연결한다.
/// 다시 실행하면 라이브러리의 Inspector 조정값이 아래 기본값으로 돌아간다. 음원 출처는 Docs/CREDITS.md.
/// </summary>
public static class GameAudioSetup
{
    const string Root = "Assets/_Project/Audio/";
    const string LibraryPath = Root + "GameSoundLibrary.asset";
    const string MixerPath = "Assets/_Project/UI/TitleMenu/GameAudio.mixer";

    [MenuItem("Tools/Scroll Hunter/Apply Game Audio")]
    public static void Apply()
    {
        ConfigureImports();
        var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (lib == null) { lib = ScriptableObject.CreateInstance<SoundLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
        Fill(lib);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssetIfDirty(lib);   // SaveAssets는 무관한 TMP 동적 폰트까지 쓰므로 쓰지 않는다
        Connect(lib);
        Debug.Log("[게임 소리] 라이브러리·씬 연결 완료");
    }

    // ───────── 반입 설정 ─────────

    static void ConfigureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = importer.defaultSampleSettings;
            bool music = path.Contains("/Music/");
            bool longClip = path.Contains("jingle") || path.Contains("loop") || path.Contains("thunder") || path.Contains("chime") || path.Contains("magic_shield");
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.75f;
            s.loadType = music ? AudioClipLoadType.Streaming : longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = !music;
            bool changed = importer.forceToMono != !music || importer.loadInBackground != music;
            importer.forceToMono = !music;   // 화면 고정 2D 소리라 효과음은 모노
            importer.loadInBackground = music;
            importer.defaultSampleSettings = s;
            if (changed || EditorUtility.IsDirty(importer)) importer.SaveAndReimport();
        }
    }

    // ───────── 기본 배정 ─────────

    static AudioClip C(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + path);
        if (clip == null) Debug.LogWarning("[게임 소리] 파일 없음: " + path);
        return clip;
    }

    static SoundCue Cue(float volume, float pitchMin, float pitchMax, float cooldown, params string[] paths)
    {
        var clips = new AudioClip[paths.Length];
        for (int i = 0; i < paths.Length; i++) clips[i] = C(paths[i]);
        return new SoundCue { clips = clips, volume = volume, pitch = new Vector2(pitchMin, pitchMax), cooldown = cooldown };
    }

    static SoundCue Cue(float volume, params string[] paths) => Cue(volume, 0.96f, 1.04f, 0f, paths);
    static SoundCue None => new SoundCue();

    static readonly string[] FireImpact = { "Magic/jc_fire_impact_01.wav", "Magic/jc_fire_impact_02.wav", "Magic/jc_fire_impact_03.wav" };
    static readonly string[] IceImpact = { "Magic/jc_ice_impact_01.wav", "Magic/jc_ice_impact_02.wav", "Magic/jc_ice_impact_03.wav" };
    static readonly string[] ElectricHit = { "Magic/jc_electric_hit_01.wav", "Magic/jc_electric_hit_02.wav", "Magic/jc_electric_hit_03.wav" };
    static readonly string[] DaggerSwing = { "Weapons/jc_dagger_swing_01.wav", "Weapons/jc_dagger_swing_02.wav", "Weapons/jc_dagger_swing_03.wav" };
    static readonly string[] HeavySwing = { "Weapons/jc_heavy_swing_01.wav", "Weapons/jc_heavy_swing_02.wav", "Weapons/jc_heavy_swing_03.wav" };
    static readonly string[] HeavyHit = { "Weapons/jc_heavy_hit_01.wav", "Weapons/jc_heavy_hit_02.wav", "Weapons/jc_heavy_hit_03.wav" };
    static readonly string[] PunchMedium = { "Impacts/punch_medium_00.ogg", "Impacts/punch_medium_01.ogg", "Impacts/punch_medium_02.ogg" };
    static readonly string[] PunchHeavy = { "Impacts/punch_heavy_00.ogg", "Impacts/punch_heavy_01.ogg", "Impacts/punch_heavy_02.ogg" };

    static void Fill(SoundLibrary lib)
    {
        // 차단 성공(침묵·제압 공유): 마력을 빨아들여 닫는 짧은 봉인음(leohpaz Absorb). 단검·유리·밝은 알림 느낌 배제.
        lib.interruptSuccess = Cue(0.85f, 0.98f, 1.02f, 0.05f, "Magic/sh_interrupt_seal.wav");
        lib.interruptSuccessLayer = None;
        lib.interruptFail = Cue(0.7f, 0.8f, 0.85f, 0.1f, "Impacts/soft_medium_00.ogg");
        lib.shieldGain = Cue(0.7f, 1f, 1f, 0.1f, "Magic/jc_magic_shield.wav");
        lib.shieldAbsorb = Cue(0.65f, 0.95f, 1.05f, 0.06f, "Impacts/plate_medium_00.ogg", "Impacts/plate_medium_01.ogg", "Impacts/plate_medium_02.ogg");
        lib.shieldBreak = Cue(0.85f, 0.95f, 1.0f, 0.1f, "Impacts/glass_medium_00.ogg", "Impacts/glass_medium_01.ogg");
        lib.playerHit = Cue(0.8f, 0.92f, 1.05f, 0.06f, "Impacts/soft_heavy_00.ogg", "Impacts/soft_heavy_01.ogg", "Impacts/soft_heavy_02.ogg");
        lib.overlapVolume = 0.6f;
        lib.potion = Cue(0.8f, 1f, 1.05f, 0.1f, "UI/potion_glass.ogg");
        lib.potionLayer = Cue(0.55f, 1f, 1f, 0.1f, "Magic/jc_healing_chime_02.wav");
        lib.reject = Cue(0.4f, 0.8f, 0.8f, 0.2f, "Impacts/soft_medium_00.ogg");
        lib.warnRed = Cue(0.9f, 0.78f, 0.78f, 0f, "Impacts/bell_heavy_short.ogg");

        // 플레이어 스킬: 계열별 공유. 첫 효과는 major, 이후 타격은 작게(minor, 간격 제한).
        SkillData S(string n)
        {
            var guid = AssetDatabase.FindAssets(n + " t:SkillData");
            foreach (var g in guid) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p) == n) return AssetDatabase.LoadAssetAtPath<SkillData>(p); }
            Debug.LogWarning("[게임 소리] 스킬 없음: " + n); return null;
        }
        lib.skills = new[]
        {
            // 불꽃
            new SkillSound { skill = S("Skill_SK01_Fireball"), major = Cue(0.85f, 1f, 1.08f, 0f, FireImpact) },
            new SkillSound { skill = S("Skill_SK04_FirePillar"), cast = Cue(0.6f, "Magic/jc_fire_buildup.wav"), major = Cue(1f, 0.85f, 0.92f, 0f, FireImpact) },
            // 얼음
            new SkillSound { skill = S("Skill_SK02_IceArrow"), castLoop = Cue(0.25f, 1f, 1f, 0f, "Magic/jc_ice_hold_loop.wav"),
                major = Cue(0.7f, 1f, 1.08f, 0f, IceImpact), minor = Cue(0.55f, 0.95f, 1.12f, 0.08f, IceImpact) },
            new SkillSound { skill = S("Skill_SK09_Blizzard"), cast = Cue(0.55f, "Magic/freeze_wind.wav"),
                major = Cue(0.85f, 0.9f, 0.95f, 0f, IceImpact), minor = Cue(0.35f, 1.0f, 1.15f, 0.12f, IceImpact) },
            // 번개
            new SkillSound { skill = S("Skill_SK05_LightningSpear"), cast = Cue(0.55f, "Magic/jc_electric_buildup.wav"), major = Cue(1f, 0.92f, 0.98f, 0f, ElectricHit) },
            new SkillSound { skill = S("Skill_SK06_MagicSpark"), castLoop = Cue(0.45f, 1f, 1f, 0f, "Magic/jc_electric_hold_loop.wav"),
                major = Cue(0.8f, 1.05f, 1.15f, 0f, ElectricHit), minor = Cue(0.5f, 1.05f, 1.2f, 0.12f, ElectricHit) },
            // 마법탄·마력
            // 매직 커터: 사용자 선택 効果音ラボ 剣で斬る3 원본 1회. 실제 피해 2타는 유지.
            new SkillSound { skill = S("Skill_SK03_MagicCutter"), major = Cue(0.9f, 1f, 1f, 0f, "Magic/sel_sword_slash3.mp3") },
            // 매직 임팩트: 사용자 선택 効果音ラボ 爆発2 원본 1회. 음량을 낮추고 피해 3타 유지.
            new SkillSound { skill = S("Skill_SK07_MagicImpact"), major = Cue(0.7f, 1f, 1f, 0f, "Magic/sel_explosion2.mp3") },
            new SkillSound { skill = S("Skill_SK08_Judgment"), castLoop = Cue(0.35f, 1.15f, 1.15f, 0f, "Magic/jc_mana_drain_loop.wav"),
                major = Cue(0.95f, 1f, 1f, 0f, "Magic/sel_beam_cannon2.mp3") },
            // 다크 이럽션: 포화 가공 없는 원본 편집본(디엔드의 sh_dark_blast와 분리).
            new SkillSound { skill = S("Skill_SK10_DarkEruption"), major = Cue(0.85f, 1f, 1.03f, 0f, "Magic/sh_dark_eruption.wav") },
            // 차단 계열: 성공/실패음은 공통. 제압은 피해 타격음만 추가.
            new SkillSound { skill = S("Skill_Silence") },
            new SkillSound { skill = S("Skill_SK12_Suppression"), major = Cue(0.45f, 0.97f, 1.03f, 0f, "Magic/sh_suppress_hit.wav") },   // 봉인음 아래 낮은 마력 충격(금속음 제거)
            // 방어·회복
            new SkillSound { skill = S("Skill_CrystalShield") },
            new SkillSound { skill = S("Skill_SK14_Sanctuary"), castLoop = Cue(0.6f, 1f, 1f, 0f, "Magic/sh_sanctuary_soft_loop.wav") },
        };

        EnemyData E(string n)
        {
            foreach (var g in AssetDatabase.FindAssets(n + " t:EnemyData", new[] { "Assets/_Project/Data" }))
            { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p) == n) return AssetDatabase.LoadAssetAtPath<EnemyData>(p); }
            Debug.LogWarning("[게임 소리] 적 데이터 없음: " + n); return null;
        }
        EnemyAttackSound Melee(string label, SoundCue swing, SoundCue hit) => new EnemyAttackSound { label = label, release = swing, releaseLayer = hit };
        var bow = new EnemyAttackSound
        {
            prepare = Cue(0.6f, "Weapons/jc_bow_draw.wav"), release = Cue(0.85f, "Weapons/jc_bow_shoot_01.wav", "Weapons/jc_bow_shoot_02.wav"),
            delayed = Cue(0.75f, "Weapons/jc_arrow_hit_01.wav", "Weapons/jc_arrow_hit_02.wav"), delaySeconds = 0.18f,   // EnemyAnimationDriver 화살 비행 시간
        };
        lib.enemies = new[]
        {
            new EnemySound { enemy = E("Enemy_Tutorial"), attacks = new[] {   // 깡패: 주먹·전력 주먹·발차기
                Melee("펀치", Cue(0.5f, 1.25f, 1.35f, 0f, HeavySwing), Cue(0.8f, 1f, 1.08f, 0f, PunchMedium)),
                Melee("전력펀치", Cue(0.6f, 1.15f, 1.2f, 0f, HeavySwing), Cue(0.9f, 0.95f, 1f, 0f, PunchHeavy)),
                Melee("발차기", Cue(0.6f, 1.05f, 1.1f, 0f, HeavySwing), Cue(0.95f, 0.85f, 0.9f, 0f, PunchHeavy)) } },
            new EnemySound { enemy = E("Enemy_A_Green"), attacks = new[] {   // 약탈자: 단검 베기
                Melee("휘두르기", Cue(0.75f, 0.95f, 1.05f, 0f, DaggerSwing), Cue(0.5f, 1f, 1.08f, 0f, "Weapons/knife_slice.ogg")) } },
            new EnemySound { enemy = E("Enemy_B_Orange"), attacks = new[] { bow, bow } },   // 궁병: 당김·발사·화살 착탄
            new EnemySound { enemy = E("Enemy_C_Red"), attacks = new[] {   // 우두머리: 대검 베기·내려치기
                Melee("참격", Cue(0.8f, 0.95f, 1f, 0f, HeavySwing), Cue(0.65f, 0.95f, 1f, 0f, HeavyHit)),
                new EnemyAttackSound { label = "회심참", release = Cue(0.9f, 0.82f, 0.86f, 0f, HeavySwing), releaseLayer = Cue(1f, 0.72f, 0.78f, 0f, PunchHeavy) } } },
            new EnemySound { enemy = E("Enemy_Boss"), attacks = new[] {   // 마법사 5종: 준비→발동, 디엔드는 축적음과 폭발 분리
                new EnemyAttackSound { label = "매직미사일", prepare = Cue(0.45f, 0.85f, 0.85f, 0f, "Magic/jc_fire_buildup.wav"),
                    release = Cue(0.7f, 0.85f, 0.9f, 0f, "Magic/jc_fire_launch.wav"), delayed = Cue(0.8f, 0.78f, 0.82f, 0f, FireImpact), delaySeconds = 0.22f },   // BossAttackVfx 투사체 비행
                new EnemyAttackSound { label = "사일런스", prepare = Cue(0.5f, 1.1f, 1.1f, 0f, "Magic/jc_mana_drain_start.wav"),
                    release = Cue(0.8f, 1.1f, 1.15f, 0f, "Magic/jc_teleport_in.wav") },
                new EnemyAttackSound { label = "다크홀", prepareLoop = Cue(0.75f, 1f, 1f, 0f, "Magic/jc_dark_chant_loop.wav"), loopRises = true,
                    release = Cue(0.9f, 0.95f, 1f, 0f, "Magic/sh_darkhole_blast_clean.wav") },   // 포화 가공 없는 원본 편집본
                new EnemyAttackSound { label = "나이트메어", prepareLoop = Cue(0.4f, 0.8f, 0.8f, 0f, "Magic/jc_mana_drain_loop.wav"),
                    release = Cue(0.8f, 0.8f, 0.85f, 0f, "Magic/jc_teleport_out.wav") },
                new EnemyAttackSound { label = "디엔드", prepareLoop = Cue(0.95f, 0.8f, 0.8f, 0f, "Magic/jc_dark_chant_loop.wav"), loopRises = true,
                    release = Cue(1f, 0.85f, 0.85f, 0f, "Magic/sh_dark_blast.wav"), releaseLayer = Cue(1f, 0.9f, 0.9f, 0f, "Magic/thunder_rumble.ogg") } } },
        };
        lib.phaseTransition = Cue(0.75f, 0.62f, 0.62f, 0.5f, "Impacts/bell_heavy_long.ogg");
        lib.orbSummon = Cue(0.6f, 0.9f, 0.95f, 0.25f, "Magic/jc_teleport_in.wav");   // 두 오브 동시 소환은 1번
        lib.orbBreak = Cue(0.85f, 0.95f, 1.05f, 0.05f, "Impacts/glass_heavy_02.ogg", "Impacts/glass_heavy_04.ogg");

        lib.click = Cue(0.5f, 0.9f, 0.9f, 0.03f, "UI/rpg_book_place.ogg");
        lib.confirm = Cue(0.65f, 0.9f, 0.9f, 0.05f, "UI/rpg_book_close.ogg");
        lib.cancel = Cue(0.4f, 0.85f, 0.85f, 0.05f, "UI/rpg_book_flip.ogg");
        lib.select = Cue(0.45f, 0.9f, 0.9f, 0.03f, "UI/rpg_leather_select.ogg");
        lib.rewardConfirm = Cue(0.8f, 0.82f, 0.82f, 0.1f, "UI/rpg_book_close.ogg");
        lib.victoryBattle = Cue(0.6f, 1f, 1f, 0.5f, "UI/lrsf_jingle_battle_win.mp3");
        lib.victoryFinal = Cue(0.65f, 1f, 1f, 0.5f, "UI/lrsf_jingle_win.mp3");
        lib.defeat = Cue(0.6f, 1f, 1f, 0.5f, "UI/lrsf_jingle_lose.mp3");

        lib.titleMusic = new MusicTrack { clip = C("Music/bgm_title_dark_shrine.wav"), volume = 0.45f };
        lib.tutorialMusic = new MusicTrack { clip = C("Music/bgm_edge_peaceful_forest.ogg"), volume = 0.25f };
        lib.battleMusic = new MusicTrack { clip = C("Music/bgm_path_iremos.ogg"), volume = 0.28f };
        lib.hideoutMusic = new MusicTrack { clip = C("Music/bgm_hideout_dark_sneaky.ogg"), volume = 0.3f };
        lib.bossMusic = new MusicTrack { clip = C("Music/bgm_boss_evil_awaits.wav"), volume = 0.32f };
    }

    // ───────── 씬 연결 ─────────

    static void Connect(SoundLibrary lib)
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;
        var audio = Object.FindFirstObjectByType<GameAudio>(FindObjectsInactive.Include);
        if (audio == null)
        {
            var go = new GameObject("GameAudio");
            audio = go.AddComponent<GameAudio>();
            Undo.RegisterCreatedObjectUndo(go, "Game Audio");
        }
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var so = new SerializedObject(audio);
        so.FindProperty("library").objectReferenceValue = lib;
        so.FindProperty("musicGroup").objectReferenceValue = mixer.FindMatchingGroups("Music")[0];
        so.FindProperty("skillGroup").objectReferenceValue = mixer.FindMatchingGroups("Skill")[0];
        so.FindProperty("enemyGroup").objectReferenceValue = mixer.FindMatchingGroups("Enemy")[0];
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        // 작업 전부터 저장 안 된 변경이 있던 씬은 덮어쓰지 않는다.
        if (!wasDirty && !Application.isPlaying) EditorSceneManager.SaveScene(scene);
        else Debug.LogWarning("[게임 소리] 씬에 기존 미저장 변경이 있어 저장하지 않음. 확인 후 직접 저장");
    }
}
