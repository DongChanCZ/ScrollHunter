using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 디자인 확인용 Game 뷰 캡처(09 디자인 확인). Play 일시정지 중 Game 뷰 해상도를 바꾸고
/// 한 프레임을 그려 PNG로 저장한다. Overlay Canvas가 실제 실행처럼 후처리 없이 찍힌다.
/// 저장 데이터는 바꾸지 않으며, 그리기 위해 진행하는 프레임 수만큼 게임 시간이 흐른다.
/// </summary>
public static class UiCapture
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static string Save(string path, int width, int height)
    {
        if (!EditorApplication.isPlaying) return "Play 모드에서 실행";
        EditorApplication.isPaused = true;
        bool resized = SetGameViewSize(width, height);
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        if (File.Exists(full)) File.Delete(full);
        // 해상도를 바꾼 첫 프레임은 Canvas 크기·월드→화면 좌표가 이전 크기로 계산되므로 먼저 두 프레임을 그린다.
        if (resized) { EditorApplication.Step(); EditorApplication.Step(); }
        ScreenCapture.CaptureScreenshot(full);
        // 캡처는 다음에 그려지는 프레임 끝에 저장된다. 파일이 생길 때까지만 진행한다(호출 뒤 상태를 바꾸기 전에 확정).
        for (int i = 0; i < 6 && !File.Exists(full); i++) EditorApplication.Step();
        return File.Exists(full) ? full : "실패: " + full;
    }

    /// <summary>Game 뷰를 고정 해상도로 바꾼다. 목록에 없으면 사용자 크기로 추가한다. 크기가 바뀌었으면 true.</summary>
    public static bool SetGameViewSize(int width, int height)
    {
        if (Screen.width == width && Screen.height == height) return false;
        Assembly editor = typeof(Editor).Assembly;
        Type viewType = editor.GetType("UnityEditor.GameView");
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type groupClass = group.GetType();
        int index = FindSize(group, width, height);
        if (index < 0)
        {
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
            object fixedKind = Enum.Parse(kindType, "FixedResolution");
            object size = Activator.CreateInstance(sizeType, fixedKind, width, height, width + "x" + height);
            groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = FindSize(group, width, height);
        }
        var view = (EditorWindow)Resources.FindObjectsOfTypeAll(viewType)[0];
        viewType.GetMethod("SizeSelectionCallback", Flags).Invoke(view, new object[] { index, null });
        return true;
    }

    private static int FindSize(object group, int width, int height)
    {
        Type groupClass = group.GetType();
        int total = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
        MethodInfo get = groupClass.GetMethod("GetGameViewSize");
        for (int i = 0; i < total; i++)
        {
            object size = get.Invoke(group, new object[] { i });
            Type t = size.GetType();
            if (t.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution"
                && (int)t.GetProperty("width").GetValue(size) == width
                && (int)t.GetProperty("height").GetValue(size) == height) return i;
        }
        return -1;
    }
}
