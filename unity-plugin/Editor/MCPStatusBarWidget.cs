using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class MCPStatusBarWidget
    {
        private static Label   _pill;
        private static VisualElement _pillContainer;
        private static VisualElement _halo;
        private static VisualElement _dot;
        private static bool    _injected;
        private static bool    _pulseHigh;
        private static IVisualElementScheduledItem _pulseItem;
        private static double  _lastHealCheck;
        private static MCPStatusModel.State _lastTickState = (MCPStatusModel.State)(-1);

        static MCPStatusBarWidget()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.delayCall += TryInject;
            EditorApplication.update    += SelfHeal;
        }

        private static void Cleanup()
        {
            BiomeLabel.Changed -= OnLabelChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            EditorApplication.update -= SelfHeal;
            try { _pulseItem?.Pause(); } catch { }
            try { _pillContainer?.RemoveFromHierarchy(); } catch { }
            _pill = null; _pillContainer = null; _halo = null; _dot = null;
            _pulseItem = null; _injected = false; _lastHealCheck = 0;
            _lastTickState = (MCPStatusModel.State)(-1);
        }

        private static void OnLabelChanged()
        {
            if (_pill == null) return;
            var state = MCPStatusModel.GetState(MCPServer.IsRunning, MCPServer.IsClientConnected,
                ChatBackendProbe.IsChatBackendRunning());
            _pill.text = MCPStatusModel.GetPill(state, MCPServer.ServerPort);
        }

        private static void SelfHeal()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastHealCheck < 1.0) return;
            _lastHealCheck = now;
            if (_pillContainer == null || _pillContainer.panel == null) TryInject();
        }

        private static void TryInject()
        {
            if (_injected && _pillContainer?.panel != null) return;
            _injected = false;
            try { _pulseItem?.Pause(); } catch { }
            _pulseItem = null;
            try
            {
                var root = GetStatusBarRoot();
                if (root == null) { EditorApplication.delayCall += TryInject; return; }
                root.Q("mcp-status-pill")?.RemoveFromHierarchy();
                _pillContainer = BuildPill();
                root.Add(_pillContainer);
                _injected = true;
                PulseTick();
                _pulseItem = _pillContainer.schedule.Execute(PulseTick).Every(900);
            }
            catch (Exception e) { Debug.LogWarning($"{BiomeLabel.Tag} StatusBar injection failed: {e.Message}"); }
        }

        private static void PulseTick()
        {
            if (_pill == null) return;
            var state = MCPStatusModel.GetState(
                MCPServer.IsRunning,
                MCPServer.IsClientConnected,
                ChatBackendProbe.IsChatBackendRunning());
            RefreshLabel(state);

            if (state != _lastTickState)
            {
                _dot.style.scale = new Scale(Vector2.one);
                _pulseHigh = false;
                _pillContainer.style.opacity = state == MCPStatusModel.State.Down ? 0.85f : 1.0f;
                var pal = MCPStatusBarPalette.Get(state, EditorGUIUtility.isProSkin);
                _halo.style.scale = new Scale(Vector2.one);
                _halo.style.opacity = state == MCPStatusModel.State.Down ? 0f : 0.18f;
                _halo.style.backgroundColor = new Color(pal.HaloRgb.r, pal.HaloRgb.g, pal.HaloRgb.b, 1f);
                _lastTickState = state;
            }

            if (state == MCPStatusModel.State.Down)
            {
                _halo.style.opacity = 0f; _halo.style.scale = new Scale(Vector2.one);
                _dot.style.scale = new Scale(Vector2.one); return;
            }

            _pulseHigh = !_pulseHigh;
            if (state == MCPStatusModel.State.Up)
            {
                _halo.style.scale   = _pulseHigh ? new Scale(new Vector2(2.4f, 2.4f)) : new Scale(Vector2.one);
                _halo.style.opacity = _pulseHigh ? 0f : 0.45f;
                _dot.style.scale    = _pulseHigh ? new Scale(Vector2.one * 1.15f) : new Scale(Vector2.one);
            }
            else // Listen
            {
                _halo.style.scale   = _pulseHigh ? new Scale(Vector2.one * 1.6f) : new Scale(Vector2.one);
                _halo.style.opacity = _pulseHigh ? 0.50f : 0.18f;
            }
        }

        private static void RefreshLabel(MCPStatusModel.State state)
        {
            var sub  = MCPServer.CurrentSubState;
            var text = MCPStatusModel.GetPill(state, sub, MCPServer.ServerPort);
            if (_pill.text == text && state == _lastTickState) return;
            _pill.text = text;
            ApplyColors(state);
        }

        private static VisualElement BuildPill()
        {
            var c = new VisualElement { name = "mcp-status-pill" };
            c.style.position = Position.Absolute; c.style.right = 90; c.style.top = 2;
            c.style.height = 16; c.style.flexDirection = FlexDirection.Row;
            c.style.alignItems = Align.Center; c.style.paddingLeft = 6; c.style.paddingRight = 7;
            Radius(c, 8); c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = 1;
            var beacon = new VisualElement { name = "mcp-beacon" };
            beacon.style.width = 12; beacon.style.height = 14; beacon.style.position = Position.Relative;
            beacon.style.alignItems = Align.Center; beacon.style.justifyContent = Justify.Center;
            beacon.style.marginRight = 5; beacon.style.overflow = Overflow.Visible;
            _halo = new VisualElement { name = "mcp-halo" };
            _halo.style.position = Position.Absolute; _halo.style.left = 2; _halo.style.top = 3;
            _halo.style.width = 8; _halo.style.height = 8; Radius(_halo, 4);
            _halo.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
            _halo.style.opacity = 0f;
            _halo.style.transitionProperty = new List<StylePropertyName> { new("scale"), new("opacity") };
            _halo.style.transitionDuration = new List<TimeValue> { new(0.85f, TimeUnit.Second), new(0.85f, TimeUnit.Second) };
            _halo.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut), new(EasingMode.EaseOut) };
            _dot = new VisualElement { name = "mcp-dot" };
            _dot.style.position = Position.Absolute; _dot.style.left = 4; _dot.style.top = 5;
            _dot.style.width = 4; _dot.style.height = 4; Radius(_dot, 2);
            _dot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
            _dot.style.opacity = 1f;
            _dot.style.transitionProperty = new List<StylePropertyName> { new("scale") };
            _dot.style.transitionDuration = new List<TimeValue> { new(0.45f, TimeUnit.Second) };
            _dot.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseInOut) };
            beacon.Add(_halo); beacon.Add(_dot);
            _pill = new Label(BiomeLabel.DisplayName) { name = "mcp-label" };
            BiomeLabel.Changed -= OnLabelChanged;
            BiomeLabel.Changed += OnLabelChanged;
            _pill.style.fontSize = 10; _pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pill.style.unityTextAlign = TextAnchor.MiddleLeft; _pill.style.marginTop = -1;
            _pill.style.backgroundColor = Color.clear; _pill.style.opacity = 1f;
            c.Add(beacon); c.Add(_pill);
            c.RegisterCallback<ClickEvent>(OnPillClick); // on the whole chip — clicks bubble up from label/beacon
            _pillContainer = c;            // ApplyColors writes _pillContainer.style — set before the call
            ApplyColors(MCPStatusModel.State.Down);
            return c;
        }

        private static void ApplyColors(MCPStatusModel.State state)
        {
            var sub = MCPServer.CurrentSubState;
            bool isErrorSub = sub is MCPStatusModel.SubState.CompileFailed
                                   or MCPStatusModel.SubState.BindFailed;
            var pal = isErrorSub
                ? MCPStatusBarPalette.GetError(EditorGUIUtility.isProSkin)
                : MCPStatusBarPalette.Get(state, EditorGUIUtility.isProSkin);
            _pillContainer.style.backgroundColor = pal.ChipBg;
            _pillContainer.style.borderTopColor  = _pillContainer.style.borderBottomColor =
            _pillContainer.style.borderLeftColor = _pillContainer.style.borderRightColor = pal.ChipBorder;
            _dot.style.backgroundColor  = pal.Dot;
            _halo.style.backgroundColor = new Color(pal.HaloRgb.r, pal.HaloRgb.g, pal.HaloRgb.b, 1f); // visibility via style.opacity, not bg alpha
            _pill.style.color = pal.Text;
        }

        private static void OnPillClick(ClickEvent _)
        {
            var phantoms = MCPServer.PhantomCount;
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Restart"),       false, MCPActions.Restart);
            m.AddItem(new GUIContent("Restart Relay"), false, MCPActions.RestartRelay);
            m.AddItem(new GUIContent("Reimport"),      false, MCPActions.Reimport);
            m.AddItem(new GUIContent("Kill (this server)"), false, MCPActions.KillCurrent);
            m.AddItem(new GUIContent("Kill All Servers"),   false, () => {
                if (EditorUtility.DisplayDialog("Kill All MCP Servers",
                    "Stop ALL running MCP servers?", "Kill All", "Cancel"))
                    MCPActions.KillAll();
            });
            if (phantoms > 0)
                m.AddItem(new GUIContent($"Kill Phantoms ({phantoms})"), false, () => MCPServer.KillPhantoms());
            else
                m.AddDisabledItem(new GUIContent("Kill Phantoms"));
            m.AddSeparator("");
            m.AddItem(new GUIContent("Open Status"), false, () => EditorApplication.ExecuteMenuItem("🧬MCP/Status"));
            m.DropDown(_pillContainer.worldBound);
        }

        private static VisualElement GetStatusBarRoot()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;
            var barType     = asm.GetType("UnityEditor.AppStatusBar");
            var guiViewType = asm.GetType("UnityEditor.GUIView");
            var backendType = asm.GetType("UnityEditor.IWindowBackend");
            if (barType == null || guiViewType == null || backendType == null) return null;
            var backendProp = guiViewType.GetProperty("windowBackend",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var treeProp = backendType.GetProperty("visualTree",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (backendProp == null || treeProp == null) return null;
            var instances = Resources.FindObjectsOfTypeAll(barType);
            if (instances == null || instances.Length == 0) return null;
            var backend = backendProp.GetValue(instances[0]);
            return backend == null ? null : treeProp.GetValue(backend) as VisualElement;
        }

        private static void Radius(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
    }
}
