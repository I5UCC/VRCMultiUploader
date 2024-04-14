using UnityEngine;
using UnityEditor;
using System;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.Core;
using System.Threading;
using System.Threading.Tasks;

namespace VRCMultiUploader
{
    [InitializeOnLoad]
    public class VrcMultiUploader : MonoBehaviour
    {
        static VrcMultiUploader()
        {
            EditorApplication.delayCall += () =>
            {
                PopupWindow.CloseAllInstances();
                EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
            };
        }

        [MenuItem("VRCMultiUploader/Build and Test All", false, 1000)]
        private static async Task BuildAndTest() => await BuildAll(true);

        [MenuItem("VRCMultiUploader/Build and Publish All", false, 1001)]
        private static async Task BuildAndUpload() => await BuildAll();

        public static async Task BuildAll(bool test = false)
        {
            PopupWindow.CloseAllInstances();
            EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");

            PipelineManager[] avatars = FindObjectsOfType<PipelineManager>();
            int avatarCount = avatars.Length;

            PopupWindow progressWindow = PopupWindow.Create(avatarCount);
            Debug.Log("Found " + avatarCount + " active Avatars in the Scene.");

            progressWindow.Progress(0, "Getting Builder...");
            if (!VRCSdkControlPanel.TryGetBuilder(out IVRCSdkAvatarBuilderApi builder))
            {
                progressWindow.Close();
                EditorUtility.DisplayDialog("MultiUploader - Error", "Could not get Builder. Please open the SDK Control Panel and try again.", "OK");
                return;
            }

            for (int i = 0; i < avatarCount; i++)
            {
                GameObject avatarObject = avatars[i].gameObject;
                string blueprintId = avatars[i].blueprintId;

                if (!test && (blueprintId == null || blueprintId == ""))
                {
                    Debug.LogError("Avatar " + avatars[i].gameObject.name + " does not have a blueprintId set.");
                    EditorUtility.DisplayDialog("MultiUploader - Error", "Avatar " + avatarObject.name + " does not have a blueprintId set."
                        + "\nPlease upload this avatar once before trying to update it with MultiUploader."
                        + "\nThis avatar will be skipped.", "OK");
                    continue;
                }

                progressWindow.Progress(i, "Uploading avatar:\n" + avatarObject.name);
                try
                {
                    if (!test)
                    {
                        Debug.Log($"Uploading avatar: {avatarObject.name} with blueprintId: {blueprintId}");
                        VRCAvatar av = await VRCApi.GetAvatar(blueprintId);
                        await builder.BuildAndUpload(avatarObject, av);
                    }
                    else
                    {
                        Debug.Log($"Uploading avatar {avatarObject.name} for testing.");
                        await builder.BuildAndTest(avatarObject);
                    }
                    if (progressWindow.cancelled)
                    {
                        progressWindow.Progress(i, $"Cancelled! ({i + 1}/{avatarCount})", true);
                        return;
                    }
                    progressWindow.Progress(i, "Finished uploading avatar:\n" + avatarObject.name, true);
                    Thread.Sleep(2000);
                }
                catch (NullReferenceException e)
                {
                    progressWindow.Close();
                    EditorUtility.DisplayDialog("MultiUploader - Error", "SDK Control Panel hasn't been opened yet. Please open the SDK Control Panel and try again.", "OK");
                    Debug.LogError(e.Message + e.StackTrace);
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message + e.StackTrace);
                }
            }

            progressWindow.Progress(avatarCount, $"Finished uploading all Avatars! ({avatarCount}/{avatarCount})", true);
            progressWindow.ShowOKButton();
        }
    }
}
