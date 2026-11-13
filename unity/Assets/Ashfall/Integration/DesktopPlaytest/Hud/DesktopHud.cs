#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.DesktopPlaytest.Hud
{
    // Passive local-sandbox presentation. Pawn owns input, pause and cursor capture.
    public sealed class DesktopHud : MonoBehaviour, IDesktopPlaytestModule
    {
        private DesktopPlaytestContext context;
        private GUIStyle textStyle, headingStyle, buttonStyle, crosshairStyle;
        private Vector2 scroll;

        public void Initialize(DesktopPlaytestContext value)
        {
            if (value == null) throw new ArgumentNullException("value");
            context = value;
            scroll = Vector2.zero;
        }

        public static float ScaleFor(int width, int height)
        {
            return Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), .75f, 1.5f);
        }

        // Shared with the native fixture; all rectangles are in scaled GUI coordinates.
        public static Rect PanelFor(float width, float height)
        {
            float margin = Mathf.Min(12, Mathf.Min(width, height) / 8);
            return new Rect(margin, margin, Mathf.Max(0, Mathf.Min(440, width - 2 * margin)),
                Mathf.Max(0, height - 2 * margin));
        }
        // Narrow gameplay windows keep their centre available for aiming. Pause
        // retains the wider scrollable controls/menu panel.
        public static Rect PlayingPanelFor(float width,float height)
        {
            Rect panel=PanelFor(width,height);
            panel.width=Mathf.Min(320,Mathf.Min(panel.width,Mathf.Max(0,width*.45f-panel.x)));
            return panel;
        }

        public static string StatusText(DesktopPlaytestContext value, CursorLockMode cursor, bool visible)
        {
            if (value == null) return "Waiting for desktop playtest context";
            return (value.Paused ? "PAUSED" : "PLAYING") + " | Cursor: " + cursor +
                (visible ? " / visible" : " / hidden") + "\n\n" +
                Safe(value.MovementStatus, "Movement status unavailable") + "\n\n" +
                "Weapon / ammo: " + Safe(value.CombatStatus, "Combat status unavailable") + "\n" +
                "Hits: " + value.Hits + " | Targets remaining: " + value.RemainingTargets + "\n\n" +
                Safe(value.ConstructionStatus, "Construction status unavailable");
        }

        private static string Safe(string value, string fallback)
        {
            if (String.IsNullOrEmpty(value)) return fallback;
            // Bound a broken publisher without interpreting its feature state. Rich text is disabled.
            return value.Length <= 2048 ? value : value.Substring(0, 2048) + "...";
        }

        private void EnsureStyles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 17;
            textStyle.wordWrap = true;
            textStyle.richText = false;
            textStyle.normal.textColor = Color.white;
            headingStyle = new GUIStyle(textStyle);
            headingStyle.fontSize = 20;
            headingStyle.fontStyle = FontStyle.Bold;
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 18;
            buttonStyle.richText = false;
            crosshairStyle = new GUIStyle(textStyle);
            crosshairStyle.alignment = TextAnchor.MiddleCenter;
            crosshairStyle.fontSize = 24;
        }

        private void OnGUI()
        {
            if (context == null || Screen.width <= 0 || Screen.height <= 0) return;
            EnsureStyles();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color, oldContent = GUI.contentColor, oldBackground = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try
            {
                float scale = ScaleFor(Screen.width, Screen.height);
                float width = Screen.width / scale, height = Screen.height / scale;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                GUI.backgroundColor = new Color(.08f, .1f, .14f, .97f);
                GUI.enabled = true; // Pause/timeScale do not disable IMGUI buttons.
                Rect panel = context.Paused?PanelFor(width,height):PlayingPanelFor(width,height);
                GUI.Box(panel, GUIContent.none);
                float inset = Mathf.Min(12, panel.width / 8);
                float innerWidth = Mathf.Max(1, panel.width - 2 * inset);
                float footerHeight = Mathf.Min(44, panel.height / 3);
                Rect viewport = new Rect(panel.x + inset, panel.y + inset, innerWidth,
                    Mathf.Max(0, panel.height - footerHeight - 3 * inset));
                float textWidth = Mathf.Max(1, innerWidth - 22);
                string heading = "ASHFALL | 4pm playtest\nLocal synthetic sandbox";
                string controls = "WASD move | Mouse look\nSpace jump | Shift sprint\nLeft click fire | R reload\nB preview / E place (if build enabled)\nEscape pause / release cursor\nControls require the corresponding live module.";
                string status = StatusText(context, Cursor.lockState, Cursor.visible);
                if(!context.Paused&&width<720)
                {
                    heading="ASHFALL test";
                    controls="Esc: menu / controls";
                    status="Hits "+context.Hits+" | targets "+context.RemainingTargets+"\nB/E: build if enabled";
                }
                float titleHeight = headingStyle.CalcHeight(new GUIContent(heading), textWidth);
                float controlsHeight = textStyle.CalcHeight(new GUIContent(controls), textWidth);
                float statusHeight = textStyle.CalcHeight(new GUIContent(status), textWidth);
                scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, textWidth,
                    titleHeight + controlsHeight + statusHeight + 32));
                try
                {
                    GUI.Label(new Rect(0, 0, textWidth, titleHeight), heading, headingStyle);
                    GUI.Label(new Rect(0, titleHeight + 12, textWidth, controlsHeight), controls, textStyle);
                    GUI.Label(new Rect(0, titleHeight + controlsHeight + 24, textWidth, statusHeight), status, textStyle);
                }
                finally { GUI.EndScrollView(); }
                float buttonWidth = Mathf.Max(0, (innerWidth - 8) / 2);
                float footerY = panel.yMax - footerHeight - inset;
                if (GUI.Button(new Rect(panel.x + inset, footerY, buttonWidth, footerHeight), "Reset", buttonStyle))
                    context.RequestReset();
                if (GUI.Button(new Rect(panel.x + inset + buttonWidth + 8, footerY, buttonWidth, footerHeight), "Quit", buttonStyle))
                    context.RequestQuit();
                if (!context.Paused)
                {
                    GUI.contentColor = Color.black;
                    GUI.Label(new Rect(width / 2 - 15, height / 2 - 15, 32, 32), "+", crosshairStyle);
                    GUI.contentColor = Color.white;
                    GUI.Label(new Rect(width / 2 - 16, height / 2 - 16, 32, 32), "+", crosshairStyle);
                }
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContent;
                GUI.backgroundColor = oldBackground;
                GUI.enabled = oldEnabled;
            }
        }

        private void OnDisable() { scroll = Vector2.zero; ReleaseStyles(); }
        private void OnDestroy() { context = null; ReleaseStyles(); }
        private void ReleaseStyles()
        {
            // GUIStyle has no Dispose; cloned styles are GC-owned. Fonts/textures belong to GUI.skin.
            textStyle = headingStyle = buttonStyle = crosshairStyle = null;
        }
    }
}
#endif
