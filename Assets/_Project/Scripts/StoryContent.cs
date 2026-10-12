using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Scroll Hunter/Story Content")]
public sealed class StoryContent : ScriptableObject
{
    [Serializable] public class IntroPage
    {
        public Texture2D image;
        public Rect visibleRegion = new Rect(0,0,1,1);
        [TextArea(3, 8)] public string text;
        public IntroPresentation.Style style;
        [Range(1f, 1.1f)] public float startZoom = 1f, endZoom = 1.04f;
        public Vector2 focusFrom = new Vector2(.5f,.5f), focusTo = new Vector2(.5f,.5f);
        public bool emphasizeLastLine;
        [Min(0f)] public float blackHold;
    }
    public IntroPresentation.Settings introPresentation = new IntroPresentation.Settings();
    [Serializable] public class Portrait { public string displayName; public Texture2D image; }
    public enum Speaker { Narrator, Player, Enemy }
    [Serializable] public class DialogueLine
    {
        public Speaker speaker;
        public int portraitIndex;
        public int secondaryPortraitIndex = -1;
        public string speakerNameOverride;
        [TextArea(2, 6)] public string text;
    }
    [Min(1f)] public float dialogueCharactersPerSecond = 50f;
    public string playerName = "모험가";
    public string continueText = "Space";
    public string introSkipText = "인트로 건너뛰기";
    public string dialogueSkipText = "대화 건너뛰기";
    public IntroPage[] intro;
    public Portrait[] portraits;
    public DialogueLine[] tutorialDialogue;
    public DialogueLine[] forestDialogue;
    public DialogueLine[] leaderDialogue;
    public DialogueLine[] banditDialogue;
    public DialogueLine[] bossDialogue;
}
