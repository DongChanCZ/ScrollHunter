using UnityEngine;

/// <summary>소리 한 종류. 여러 파일 중 하나를 무작위로 고르고, 음높이는 범위 안에서 흔든다(게임 배속과 무관).</summary>
[System.Serializable]
public class SoundCue
{
    public AudioClip[] clips = new AudioClip[0];
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("무작위 음높이 범위(x~y). 배속에 따라 바뀌지 않는다")]
    public Vector2 pitch = Vector2.one;
    [Tooltip("같은 소리를 다시 내기까지 최소 간격(실제 초). 광역·다단히트·여러 적의 과다 중복 방지")]
    [Min(0f)] public float cooldown;
    public bool IsEmpty => clips == null || clips.Length == 0;
}

/// <summary>플레이어 스킬 1종의 소리. 필요한 칸만 채운다.</summary>
[System.Serializable]
public class SkillSound
{
    public SkillData skill;
    [Tooltip("입력 수락·시전 시작 때 1회. 시전이 끝나거나 취소되면 멈춘다(준비음)")]
    public SoundCue cast = new SoundCue();
    [Tooltip("시전·채널링 동안 반복. 끝나거나 취소되면 짧게 줄이며 멈춘다")]
    public SoundCue castLoop = new SoundCue();
    [Tooltip("이번 사용의 첫 실제 효과(첫 타격). 같은 프레임 여러 대상은 1번만")]
    public SoundCue major = new SoundCue();
    [Tooltip("이후 타격(다단히트·채널링 반복). 비우면 major 사용, cooldown으로 누적 제한")]
    public SoundCue minor = new SoundCue();
}

/// <summary>적 공격 1개(EnemyData 공격 목록 순서)의 소리.</summary>
[System.Serializable]
public class EnemyAttackSound
{
    public string label;
    [Tooltip("시전 시작 때 1회. 차단·사망·전환·종료 때 멈춘다")]
    public SoundCue prepare = new SoundCue();
    [Tooltip("시전 동안 반복. 차단·사망·발동 때 멈춘다")]
    public SoundCue prepareLoop = new SoundCue();
    [Tooltip("켜면 반복음이 시전 진행에 따라 커진다(축적음)")]
    public bool loopRises;
    [Tooltip("실제 발동 순간(모델 타격 자세와 같은 프레임)")]
    public SoundCue release = new SoundCue();
    public SoundCue releaseLayer = new SoundCue();
    [Tooltip("발동 뒤 지연 재생(화살·투사체 착탄). 게임 시간 기준")]
    public SoundCue delayed = new SoundCue();
    [Min(0f)] public float delaySeconds;
}

[System.Serializable]
public class EnemySound
{
    public EnemyData enemy;
    public EnemyAttackSound[] attacks = new EnemyAttackSound[0];
}

[System.Serializable]
public class MusicTrack
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.35f;
}

/// <summary>
/// 게임 소리 목록(10/8, 10 A48). GameAudio가 읽는다. 출력 그룹은 Music(배경음)·Skill(플레이어·공통 UI)·Enemy(적·보스·오브).
/// 메뉴 Tools > Scroll Hunter > Apply Game Audio가 기본값으로 채운다(다시 실행하면 Inspector 조정값을 덮어씀).
/// </summary>
[CreateAssetMenu(menuName = "Scroll Hunter/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    [Header("공통 전투 (Skill 그룹)")]
    public SoundCue interruptSuccess = new SoundCue();
    public SoundCue interruptSuccessLayer = new SoundCue();
    public SoundCue interruptFail = new SoundCue();
    public SoundCue shieldGain = new SoundCue();
    public SoundCue shieldAbsorb = new SoundCue();
    public SoundCue shieldBreak = new SoundCue();
    public SoundCue playerHit = new SoundCue();
    [Tooltip("방어도 흡수와 HP 피해가 같은 타격에 겹칠 때 흡수·소진음 음량 배율")]
    [Range(0f, 1f)] public float overlapVolume = 0.6f;
    public SoundCue potion = new SoundCue();
    public SoundCue potionLayer = new SoundCue();
    public SoundCue reject = new SoundCue();

    [Header("적 경고 (Enemy 그룹, 시전 시작 1회)")]
    public SoundCue warnRed = new SoundCue();

    [Header("플레이어 스킬 (Skill 그룹)")]
    public SkillSound[] skills = new SkillSound[0];

    [Header("적·보스 (Enemy 그룹)")]
    public EnemySound[] enemies = new EnemySound[0];
    public SoundCue phaseTransition = new SoundCue();
    public SoundCue orbSummon = new SoundCue();
    public SoundCue orbBreak = new SoundCue();

    [Header("UI (Skill 그룹, 정지 중에도 재생)")]
    public SoundCue click = new SoundCue();
    public SoundCue confirm = new SoundCue();
    public SoundCue cancel = new SoundCue();
    public SoundCue select = new SoundCue();
    public SoundCue rewardConfirm = new SoundCue();
    public SoundCue victoryBattle = new SoundCue();
    public SoundCue victoryFinal = new SoundCue();
    public SoundCue defeat = new SoundCue();

    [Header("배경음 (Music 그룹)")]
    public MusicTrack titleMusic = new MusicTrack();
    public MusicTrack tutorialMusic = new MusicTrack();
    [Tooltip("인적 드문 오솔길")]
    public MusicTrack battleMusic = new MusicTrack();
    public MusicTrack hideoutMusic = new MusicTrack();
    public MusicTrack bossMusic = new MusicTrack();
}
