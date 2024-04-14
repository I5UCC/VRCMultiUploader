using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Reflection;

namespace VRCMultiUploader
{
    public class PopupWindow : EditorWindow
    {
        private Label toplabel;
        private Label label;
        private Button ok_button;
        private Button cancel_button;
        private ProgressBar progress;
        private VisualElement infoBox;
        private static int amountofAvatars;
        public bool cancelled = false;

        public static PopupWindow Create(int amount)
        {
            amountofAvatars = amount;
            var window = CreateInstance<PopupWindow>();
            var mainWindowPos = GetEditorMainWindowPos();
            var size = new Vector2(500, 125);
            window.position = new Rect(mainWindowPos.xMin + (mainWindowPos.width - size.x) * 0.5f, mainWindowPos.yMax * 0.5f + 150, size.x, size.y);
            window.ShowPopup();
            return window;
        }

        public static void CloseAllInstances()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<PopupWindow>())
            {
                if (w) w.Close();
            }
        }

        public void OnEnable()
        {
            var root = rootVisualElement;

            infoBox = new VisualElement();
            root.Add(infoBox);

            toplabel = new Label("- VRCMultiUploader v0.1.4 by I5UCC -");
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

            cancel_button = new Button(() =>
            {
                cancelled = true;
                ShowOKButton();
            });
            cancel_button.text = "X";
            cancel_button.style.width = 20;
            cancel_button.style.height = 20;
            cancel_button.style.position = Position.Absolute;
            cancel_button.style.right = 0;
            cancel_button.style.top = 2;
            infoBox.Add(cancel_button);
        }

        public void ShowOKButton()
        {
            ok_button = new Button(() => Close());
            ok_button.text = "OK";
            ok_button.style.width = 200;
            ok_button.style.fontSize = 16;
            ok_button.style.marginTop = 17;
            ok_button.style.marginLeft = 20;
            ok_button.style.marginRight = 20;
            ok_button.style.unityTextAlign = TextAnchor.MiddleCenter;
            ok_button.style.alignSelf = Align.Center;

            infoBox.Remove(progress);
            infoBox.Remove(cancel_button);
            infoBox.Add(ok_button);
            RepaintNow();
        }

        public void Progress(int current, string info, bool finished = false)
        {
            current += 1;
            label.text = info;
            progress.value = (float)(current - 1) / amountofAvatars * 100;
            int percent = (int)Math.Round(progress.value);
            if (current > amountofAvatars) current = amountofAvatars;
            progress.title = $"{current}/{amountofAvatars} ({percent}%)";
            Debug.Log($"Progress ({current}/{amountofAvatars}): {info}");
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

}