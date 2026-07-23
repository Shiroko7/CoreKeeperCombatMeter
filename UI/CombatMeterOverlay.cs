using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// OnGUI overlay window for the combat meter. Follows the same MonoBehaviour +
/// DontDestroyOnLoad + OnGUI pattern as the community DebugMod's FPS overlay, which is
/// the proven way to draw a persistent UI window in this game outside of the ECS/UI Toolkit
/// pipeline the base game itself uses. Visual style is modeled after WoW-style damage
/// meters (Skada/Details!): dark rounded card, rank-colored bars filled left-to-right
/// with the name/number/percentage baked into the bar itself. Uses Unity 6's native
/// rounded-corner GUI.DrawTexture overload (ScaleMode, alphaBlend, aspect, color,
/// borderWidth, borderRadius) instead of hard-edged rects for every drawn shape.
/// </summary>
internal sealed class CombatMeterOverlay : MonoBehaviour
{
    private enum Tab
    {
        Dealt,
        Received,
        Healing,
        History,
    }

    private const float WindowWidth = 380f;
    private const float WindowHeight = 370f;
    private const float Margin = 5f;
    private const float ShadowOffset = 3f;
    private const float PanelRadius = 10f;
    private const float ControlRadius = 5f;
    private const float BarRadius = 5f;
    private const float HeaderHeight = 26f;
    private const float TabHeight = 22f;
    private const float BarHeight = 22f;
    private const float BarSpacing = 4f;
    private const float ContentPadding = 10f;

    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.45f);
    private static readonly Color PanelColor = new(0.075f, 0.078f, 0.095f, 0.97f);
    private static readonly Color DividerColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color TabIdleColor = new(1f, 1f, 1f, 0.05f);
    private static readonly Color TabSelectedColor = new(0.30f, 0.45f, 0.68f, 0.9f);
    private static readonly Color ButtonIdleColor = new(1f, 1f, 1f, 0.07f);
    private static readonly Color CloseButtonColor = new(0.55f, 0.22f, 0.22f, 0.55f);
    private static readonly Color RowAltColor = new(1f, 1f, 1f, 0.025f);
    private static readonly Color BarBackgroundColor = new(1f, 1f, 1f, 0.05f);

    private static readonly Color[] BarColors =
    {
        new(0.36f, 0.62f, 0.92f),
        new(0.86f, 0.44f, 0.36f),
        new(0.45f, 0.78f, 0.45f),
        new(0.86f, 0.72f, 0.30f),
        new(0.62f, 0.45f, 0.82f),
        new(0.40f, 0.78f, 0.78f),
        new(0.85f, 0.55f, 0.75f),
        new(0.65f, 0.70f, 0.40f),
    };

    private static CombatMeterOverlay _instance;

    private Rect _windowRect = new(80f, 80f, WindowWidth, WindowHeight);
    private Tab _tab = Tab.Dealt;
    private EncounterRecord _viewedHistoryEntry;
    private Texture2D _whiteTexture;

    private GUIStyle _titleStyle;
    private GUIStyle _buttonLabelStyle;
    private GUIStyle _tabLabelStyle;
    private GUIStyle _tabLabelSelectedStyle;
    private GUIStyle _encounterHeaderStyle;
    private GUIStyle _encounterSubHeaderStyle;
    private GUIStyle _nameStyle;
    private GUIStyle _numberStyle;
    private GUIStyle _rankStyle;
    private GUIStyle _emptyStyle;
    private GUIStyle _historyNameStyle;
    private GUIStyle _historyMetaStyle;
    private bool _stylesReady;

    internal static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        var go = new GameObject("[CombatMeter] Overlay");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CombatMeterOverlay>();
        _instance.enabled = false;
    }

    internal static string Toggle()
    {
        EnsureInstance();
        _instance.enabled = !_instance.enabled;
        return _instance.enabled ? "Combat meter shown." : "Combat meter hidden.";
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _whiteTexture = Texture2D.whiteTexture;
    }

    private void Update()
    {
        CombatLogAggregator.Instance.Tick();
    }

    private void OnGUI()
    {
        EnsureStyles();
        // GUI.Window (not GUILayout.Window): the window uses a fixed, explicitly managed
        // rect. GUILayout.Window auto-sizes from top-level layout calls, and since this
        // window's content lives inside a GUILayout.BeginArea (a fixed-size island that
        // doesn't feed the outer auto-layout pass), that collapsed it to near-zero size.
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "", GUIStyle.none);
    }

    private void EnsureStyles()
    {
        if (_stylesReady)
        {
            return;
        }

        _stylesReady = true;

        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _titleStyle.normal.textColor = new Color(0.93f, 0.93f, 0.96f);

        _buttonLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _buttonLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);

        _tabLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
        _tabLabelStyle.normal.textColor = new Color(0.62f, 0.62f, 0.68f);

        _tabLabelSelectedStyle = new GUIStyle(_tabLabelStyle) { fontStyle = FontStyle.Bold };
        _tabLabelSelectedStyle.normal.textColor = Color.white;

        _encounterHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        _encounterHeaderStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);

        _encounterSubHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
        _encounterSubHeaderStyle.normal.textColor = new Color(0.6f, 0.6f, 0.66f);

        _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _nameStyle.normal.textColor = Color.white;
        _nameStyle.padding.left = 8;

        _numberStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
        _numberStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        _numberStyle.padding.right = 8;

        _rankStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
        _rankStyle.normal.textColor = new Color(1f, 1f, 1f, 0.32f);

        _emptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
        _emptyStyle.normal.textColor = new Color(0.55f, 0.55f, 0.6f);

        _historyNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        _historyNameStyle.normal.textColor = Color.white;

        _historyMetaStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
        _historyMetaStyle.normal.textColor = new Color(0.6f, 0.6f, 0.66f);
    }

    private void DrawWindow(int windowId)
    {
        var panelRect = new Rect(Margin, Margin, WindowWidth - Margin * 2f, WindowHeight - Margin * 2f);
        DrawRoundedRect(new Rect(panelRect.x + ShadowOffset, panelRect.y + ShadowOffset, panelRect.width, panelRect.height), ShadowColor, PanelRadius);
        DrawRoundedRect(panelRect, PanelColor, PanelRadius);

        DrawHeaderRow(panelRect);
        DrawFilledRect(new Rect(panelRect.x + ContentPadding, panelRect.y + HeaderHeight, panelRect.width - ContentPadding * 2f, 1f), DividerColor);

        var content = new Rect(
            panelRect.x + ContentPadding,
            panelRect.y + HeaderHeight + 6f,
            panelRect.width - ContentPadding * 2f,
            panelRect.height - HeaderHeight - ContentPadding - 6f);

        GUILayout.BeginArea(content);
        DrawTabs();
        GUILayout.Space(6f);

        EncounterRecord shown = _tab == Tab.History ? null : (_viewedHistoryEntry ?? CombatLogAggregator.Instance.Current);

        if (_tab == Tab.History)
        {
            DrawHistoryList();
        }
        else if (shown == null)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("No active encounter yet - go fight something.", _emptyStyle);
            GUILayout.FlexibleSpace();
        }
        else
        {
            DrawEncounterHeader(shown);
            Dictionary<string, SourceStats> table = _tab switch
            {
                Tab.Dealt => shown.Dealt,
                Tab.Received => shown.Received,
                Tab.Healing => shown.Healing,
                _ => shown.Dealt,
            };
            DrawBars(table, shown.Duration);
        }

        GUILayout.EndArea();

        GUI.DragWindow(new Rect(panelRect.x, panelRect.y, panelRect.width, HeaderHeight));
    }

    private void DrawHeaderRow(Rect panelRect)
    {
        var titleRect = new Rect(panelRect.x + ContentPadding, panelRect.y, 160f, HeaderHeight);
        GUI.Label(titleRect, "Combat Meter", _titleStyle);

        float buttonSize = HeaderHeight - 8f;
        var closeRect = new Rect(panelRect.xMax - ContentPadding - buttonSize, panelRect.y + 4f, buttonSize, buttonSize);
        DrawRoundedRect(closeRect, CloseButtonColor, ControlRadius);
        GUI.Label(closeRect, "x", _buttonLabelStyle);
        if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none))
        {
            enabled = false;
        }

        var resetRect = new Rect(closeRect.x - 8f - 52f, panelRect.y + 4f, 52f, buttonSize);
        DrawRoundedRect(resetRect, ButtonIdleColor, ControlRadius);
        GUI.Label(resetRect, "Reset", _buttonLabelStyle);
        if (GUI.Button(resetRect, GUIContent.none, GUIStyle.none))
        {
            CombatLogAggregator.Instance.ResetCurrent();
            _viewedHistoryEntry = null;
        }
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal(GUILayout.Height(TabHeight));
        DrawTabButton(Tab.Dealt, "Dealt");
        GUILayout.Space(4f);
        DrawTabButton(Tab.Received, "Received");
        GUILayout.Space(4f);
        DrawTabButton(Tab.Healing, "Healing");
        GUILayout.Space(4f);
        DrawTabButton(Tab.History, "History");
        GUILayout.EndHorizontal();
    }

    private void DrawTabButton(Tab tab, string label)
    {
        bool selected = _tab == tab;
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), _tabLabelStyle, GUILayout.Height(TabHeight), GUILayout.MinWidth(60f));
        DrawRoundedRect(rect, selected ? TabSelectedColor : TabIdleColor, ControlRadius);
        GUI.Label(rect, label, selected ? _tabLabelSelectedStyle : _tabLabelStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && !selected)
        {
            _tab = tab;
            if (tab != Tab.History)
            {
                _viewedHistoryEntry = null;
            }
        }
    }

    private void DrawEncounterHeader(EncounterRecord encounter)
    {
        string status = encounter.IsActive ? "In Progress" : "Ended";
        GUILayout.Label(encounter.Name, _encounterHeaderStyle);
        GUILayout.Label($"{status} - {encounter.Duration:0.0}s", _encounterSubHeaderStyle);
        GUILayout.Space(4f);
    }

    private void DrawHistoryList()
    {
        List<EncounterRecord> history = CombatLogAggregator.Instance.History;
        if (history.Count == 0)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("No past encounters yet.", _emptyStyle);
            GUILayout.FlexibleSpace();
            return;
        }

        foreach (EncounterRecord entry in history)
        {
            long total = entry.Dealt.Values.Sum(s => s.TotalAmount);
            Rect rect = GUILayoutUtility.GetRect(1f, 36f, GUILayout.ExpandWidth(true));
            DrawRoundedRect(rect, ButtonIdleColor, ControlRadius);

            var nameRect = new Rect(rect.x + 10f, rect.y + 3f, rect.width - 20f, 18f);
            GUI.Label(nameRect, entry.Name, _historyNameStyle);

            var metaRect = new Rect(rect.x + 10f, rect.y + 19f, rect.width - 20f, 14f);
            GUI.Label(metaRect, $"{entry.Duration:0.0}s   -   {FormatNumber(total)} dealt", _historyMetaStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _viewedHistoryEntry = entry;
                _tab = Tab.Dealt;
            }

            GUILayoutUtility.GetRect(1f, 4f);
        }
    }

    private void DrawBars(Dictionary<string, SourceStats> table, float duration)
    {
        if (table.Count == 0)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("No data yet.", _emptyStyle);
            GUILayout.FlexibleSpace();
            return;
        }

        List<SourceStats> rows = table.Values.OrderByDescending(s => s.TotalAmount).ToList();
        long max = rows[0].TotalAmount;
        long total = rows.Sum(s => s.TotalAmount);
        float safeDuration = Mathf.Max(duration, 1f);

        for (int i = 0; i < rows.Count; i++)
        {
            SourceStats row = rows[i];
            Rect rowRect = GUILayoutUtility.GetRect(1f, BarHeight, GUILayout.ExpandWidth(true));

            if (i % 2 == 1)
            {
                DrawFilledRect(rowRect, RowAltColor);
            }

            DrawRoundedRect(rowRect, BarBackgroundColor, BarRadius);

            float fraction = max > 0 ? (float)row.TotalAmount / max : 0f;
            Color color = BarColors[i % BarColors.Length];
            float fillWidth = Mathf.Max(rowRect.width * fraction, rowRect.height);
            var fillRect = new Rect(rowRect.x, rowRect.y, fillWidth, rowRect.height);
            float fillRadius = Mathf.Min(BarRadius, fillRect.width / 2f, fillRect.height / 2f);
            DrawRoundedRect(fillRect, color, fillRadius);

            var rankRect = new Rect(rowRect.x, rowRect.y, 20f, rowRect.height);
            GUI.Label(rankRect, (i + 1).ToString(), _rankStyle);

            var nameRect = new Rect(rowRect.x + 20f, rowRect.y, rowRect.width - 20f, rowRect.height);
            GUI.Label(nameRect, row.Name, _nameStyle);

            float percent = total > 0 ? 100f * row.TotalAmount / total : 0f;
            float perSecond = row.TotalAmount / safeDuration;
            string numberText = $"{FormatNumber(row.TotalAmount)}  ({percent:0.#}%)  {FormatNumber((long)perSecond)}/s";
            GUI.Label(rowRect, numberText, _numberStyle);

            GUILayoutUtility.GetRect(1f, BarSpacing);
        }
    }

    private void DrawFilledRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _whiteTexture);
        GUI.color = prev;
    }

    private void DrawRoundedRect(Rect rect, Color color, float radius)
    {
        GUI.DrawTexture(rect, _whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
    }

    private static string FormatNumber(long value)
    {
        if (value >= 1_000_000)
        {
            return $"{value / 1_000_000f:0.##}M";
        }

        if (value >= 1_000)
        {
            return $"{value / 1_000f:0.#}K";
        }

        return value.ToString();
    }
}
