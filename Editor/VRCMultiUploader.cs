using UnityEngine;
using UnityEditor;
using System;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.Core;
using System.Threading;
using System.Threading.Tasks;
using VRC.SDKBase.Editor;

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

            builder.OnSdkBuildProgress += (sender, message) => progressWindow.SetBottomLabel(message);
            builder.OnSdkBuildFinish += (sender, message) => progressWindow.SetBottomLabel("Uploading...", Color.yellow);
            // builder.OnSdkUploadProgress += (sender, message) => progressWindow.SetBottomLabel($"{message.status} {(int)(message.percentage * 100)}%"); // Makes the Upload Process fail for some reason?

            CancellationTokenSource cts = new();
            progressWindow.SetCancelEvent(() =>
            {
                progressWindow.ShowOKButton();
                progressWindow.cancelled = true;
                progressWindow.Progress(0, $"Cancelled!", true);
                builder.CancelUpload();
            });

            bool success = false;

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
                        await builder.BuildAndUpload(avatarObject, av, cancellationToken: cts.Token);
                        success = true;
                    }
                    else
                    {
                        Debug.Log($"Uploading avatar {avatarObject.name} for testing.");
                        await builder.BuildAndTest(avatarObject);
                        success = true;
                    }
                    progressWindow.Progress(i, "Finished uploading avatar:\n" + avatarObject.name, true);
                }
                catch (NullReferenceException e)
                {
                    progressWindow.Close();
                    EditorUtility.DisplayDialog("MultiUploader - Error", "SDK Control Panel hasn't been opened yet. Please open the SDK Control Panel and try again.", "OK");
                    Debug.LogError(e.Message + e.StackTrace);
                    success = false;
                    return;
                }
                catch (BuilderException e)
                {
                    progressWindow.SetBottomLabel("Upload Cancelled", new Color(255, 165, 0));
                    progressWindow.cancelled = true;
                    Thread.Sleep(4000);
                    Debug.LogError(e.Message + e.StackTrace);
                    if (e.Message.Contains("Avatar validation failed"))
                    {
                        EditorUtility.DisplayDialog("MultiUploader - Validation Error", "Validation Failed for " + avatarObject.name + ".\nPlease fix the errors and try again.", "OK");
                    }
                    success = false;
                }
                catch (ValidationException e)
                {
                    EditorUtility.DisplayDialog("MultiUploader - Error", e.Message + "\n" + string.Join("\n", e.Errors), "OK");
                    Debug.LogError(e.Message + e.StackTrace);
                    EditorUtility.DisplayDialog("MultiUploader - Validation Error", "Please fix the errors and try again.", "OK");
                    success = false;
                }
                catch (OwnershipException e)
                {
                    EditorUtility.DisplayDialog("MultiUploader - Error", e.Message, "OK");
                    Debug.LogError(e.Message + e.StackTrace);
                    success = false;
                }
                catch (UploadException e)
                {
                    EditorUtility.DisplayDialog("MultiUploader - Error", e.Message, "OK");
                    Debug.LogError(e.Message + e.StackTrace);
                    success = false;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message + e.StackTrace);
                    EditorUtility.DisplayDialog("MultiUploader - Unexpected Error", e.Message + "\n" + e.StackTrace, "OK");
                    success = false;
                }
                finally
                {
                    if (!success)
                    {
                        progressWindow.SetBottomLabel("Upload Failed", Color.red);
                        Thread.Sleep(4000);
                    }
                    else
                    {
                        progressWindow.SetBottomLabel("Upload Successful!", Color.green);
                        Thread.Sleep(2000);
                    }
                }
                if (progressWindow.cancelled) return;
            }

            progressWindow.Progress(avatarCount, $"Finished uploading all Avatars! ({avatarCount}/{avatarCount})", true);
            progressWindow.ShowOKButton();
            cts.Dispose();
        }
    }
}
