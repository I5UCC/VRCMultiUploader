using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Reflection;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.Core;
using System.Threading;

#if UNITY_EDITOR
public class VRCMultiUploader : MonoBehaviour
{
    [MenuItem("VRCMultiUploader/Build and Test")]
    private static void BuildAndTest()
    {
        BuildAndUploadAll(true);
    }

    [MenuItem("VRCMultiUploader/Build and Publish")]
    private static void BuildAndUpload()
    {
        BuildAndUploadAll();
    }

    public static async void BuildAndUploadAll(bool test = false)
    {
        if (SceneView.HasOpenInstances<VRCSdkControlPanel>())
            SceneView.FocusWindowIfItsOpen(typeof(VRCSdkControlPanel));
        else
            SceneView.CreateWindow<VRCSdkControlPanel>();

        GameObject avatarObject = null;
        string blueprintId = null;
        PipelineManager[] avatars = FindObjectsOfType<PipelineManager>();
        int avatarCount = avatars.Length;

        Debug.Log("Uploading " + avatarCount + " avatars in the Scene");
        VRCMultiUploader_PopupWindow progressWindow = VRCMultiUploader_PopupWindow.Create(avatarCount);
        progressWindow.Progress(0, "Starting upload process");
        Thread.Sleep(2000);

        for (int i = 0; i < avatarCount; i++)
        {
            avatarObject = avatars[i].gameObject;
            blueprintId = avatars[i].blueprintId;
            if (!test && (blueprintId == null || blueprintId == ""))
            {
                Debug.LogError("Avatar " + avatars[i].gameObject.name + " does not have a blueprintId set.");
                EditorUtility.DisplayDialog("MultiUploader - Error", "Avatar " + avatarObject.name + " does not have a blueprintId set.\nPlease upload this avatar once before trying to update it with MultiUploader.\nThis avatar will be skipped.", "OK");
                continue;
            }
            progressWindow.Progress(i + 1, "Uploading avatar:\n" + avatarObject.name);

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                Debug.LogError("Could not get builder");
                return;
            }

            try
            {
                if (!test)
                {
                    Debug.Log("Uploading avatar: " + avatarObject.name + " with blueprintId: " + blueprintId);
                    VRCAvatar av = await VRCApi.GetAvatar(blueprintId);
                    await builder.BuildAndUpload(avatarObject, av);
                }
                else
                {
                    Debug.Log("Uploading avatar " + avatarObject.name + " for testing.");
                    await builder.BuildAndTest(avatarObject);
                }
                progressWindow.Progress(i + 1, "Finished uploading avatar:\n" + avatarObject.name, true);
                Thread.Sleep(2000);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
        progressWindow.Progress(avatarCount, "Finished uploading all Avatars!", true);
        Thread.Sleep(2000);
        progressWindow.Close();
    }
}

[InitializeOnLoad]
public class VRCMultiUploader_PopupWindow : EditorWindow
{
    private Label toplabel;
    private Label label;
    private ProgressBar progress;
    private static int amountofAvatars;

    static VRCMultiUploader_PopupWindow()
    {
        EditorApplication.delayCall += () =>
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<VRCMultiUploader_PopupWindow>())
            {
                if (w) w.Close();
            }
        };
    }

    public static VRCMultiUploader_PopupWindow Create(int amount)
    {
        amountofAvatars = amount;
        var window = CreateInstance<VRCMultiUploader_PopupWindow>();
        var mainWindowPos = GetEditorMainWindowPos();
        var size = new Vector2(500, 125);
        window.position = new Rect(mainWindowPos.xMin + (mainWindowPos.width - size.x) * 0.5f, mainWindowPos.yMax * 0.5f + 150, size.x, size.y);
        window.ShowPopup();
        return window;
    }

    public void OnEnable()
    {
        var root = rootVisualElement;

        var infoBox = new VisualElement();
        root.Add(infoBox);
        toplabel = new Label("- VRCMultiUploader by I5UCC -");
        toplabel.style.paddingTop = 7;
        toplabel.style.paddingBottom = 10;
        toplabel.style.paddingLeft = 20;
        toplabel.style.paddingRight = 20;
        toplabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        toplabel.style.fontSize = 12;
        infoBox.Add(toplabel);

        progress = new ProgressBar();
        progress.style.paddingTop = 0;
        progress.style.paddingLeft = 20;
        progress.style.paddingRight = 20;
        progress.style.paddingBottom = 0;
        progress.style.fontSize = 14;
        infoBox.Add(progress);

        label = new Label("");
        label.style.paddingTop = 5;
        label.style.paddingBottom = 0;
        label.style.paddingLeft = 20;
        label.style.paddingRight = 20;
        label.style.fontSize = 24;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        infoBox.Add(label);
    }

    public void Progress(int current, string info, bool finished = false)
    {
        Debug.Log($"Progress ({current}/{amountofAvatars}): {info}");
        label.text = info;
        progress.value = (float)current / amountofAvatars * 100;
        int percent = (int)Math.Round(progress.value);
        if (percent == 100 && !finished)
        {
            percent = 99;
            progress.value = 99;
        }
        progress.title = $"{current}/{amountofAvatars} ({percent}%)";
        RepaintNow();
    }

    private void RepaintNow()
    {
        GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(this, new object[] { });
    }

    private static Type[] GetAllDerivedTypes(System.AppDomain aAppDomain, System.Type aType)
    {
        var result = new List<Type>();
        var assemblies = aAppDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(aType))
                    result.Add(type);
            }
        }
        return result.ToArray();
    }

    public static Rect GetEditorMainWindowPos()
    {
        if (Application.isBatchMode)
        {
            return Rect.zero;
        }

        var containerWinType = GetAllDerivedTypes(System.AppDomain.CurrentDomain, typeof(ScriptableObject))
            .Where(t => t.Name == "ContainerWindow")
            .FirstOrDefault();
        if (containerWinType == null)
            throw new System.MissingMemberException("Can't find internal type ContainerWindow. Maybe something has changed inside Unity");
        var showModeField = containerWinType.GetField("m_ShowMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var positionProperty = containerWinType.GetProperty("position", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (showModeField == null || positionProperty == null)
            throw new System.MissingFieldException("Can't find internal fields 'm_ShowMode' or 'position'. Maybe something has changed inside Unity");
        var windows = Resources.FindObjectsOfTypeAll(containerWinType);
        foreach (var win in windows)
        {
            var showmode = (int)showModeField.GetValue(win);
            if (showmode == 4) // main window
            {
                var pos = (Rect)positionProperty.GetValue(win, null);
                return pos;
            }
        }
        throw new System.NotSupportedException("Can't find internal main window. Maybe something has changed inside Unity");
    }
}
#endif