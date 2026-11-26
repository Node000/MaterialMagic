using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns only the second-floor HUD's presentation; the battle host still owns its contents and rules.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SecondFloorBattleHudMotion : MonoBehaviour
{
    private enum Mode { Exploring, Suspended, PreparingBattle, EnteringBattle, Battle }
    private enum BattleInput { Full, HoverOnly, None }
    private enum ExplorationReveal { None, Hand, Play, Magic }

    [Serializable]
    private sealed class Panel
    {
        [Tooltip("Move this RectTransform, not its layout-driven descendants. Keep the host outside all bound roots.")]
        public RectTransform motionRoot;
        [Tooltip("CanvasGroup on Motion Root. If omitted, use the group already on that root.")]
        public CanvasGroup visibility;
        [Tooltip("Input allowed after battle entry. Decorative roots should use None.")]
        public BattleInput battleInput = BattleInput.Full;
    }

    [Serializable]
    private sealed class BattleOnlyPanel
    {
        public Panel panel = new Panel();
        [Tooltip("Starting displacement in the panel parent's local UI units.")]
        public Vector2 enterOffset = new Vector2(0f, -400f);
        [Tooltip("Optional exploration region that also reveals this panel.")]
        public ExplorationReveal explorationReveal;
        [Tooltip("Keep existing detail hover raycasts in exploration, but not Selectable interaction.")]
        public bool explorationHoverDetails;
    }

    [Header("Second-floor HUD roots")]
    [SerializeField] private Panel hand = new Panel();
    [SerializeField] private Panel play = new Panel();
    [SerializeField] private Panel enemy = new Panel();
    [SerializeField] private Panel magic = new Panel();
    [SerializeField] private BattleOnlyPanel[] battleOnlyPanels = Array.Empty<BattleOnlyPanel>();
    [Tooltip("The actual MagicBook slots in left-to-right order. Do not reparent them out of their layout.")]
    [SerializeField] private RectTransform[] magicSlots = Array.Empty<RectTransform>();

    [Header("PC exploration hover (bottom-left screen origin)")]
    [Tooltip("Normalized rectangle within the lower half of the screen. Include the revealed panel where relevant.")]
    [SerializeField] private Rect handHoverRegion = new Rect(0.23f, 0f, 0.54f, 0.13f);
    [SerializeField] private Rect playHoverRegion = new Rect(0.23f, 0.23f, 0.54f, 0.22f);
    [SerializeField] private Rect magicHoverRegion = new Rect(0.23f, 0.13f, 0.54f, 0.10f);
    [SerializeField, Min(0f)] private float hoverSlideDistance = 110f;
    [SerializeField, Min(0f)] private float hoverSeconds = 0.17f;

    [Header("Battle entrance (local UI units)")]
    [SerializeField, Min(0f)] private float handEntranceDistance = 500f;
    [SerializeField, Min(0f)] private float playEntranceDistance = 620f;
    [SerializeField, Min(0f)] private float enemyEntranceDistance = 620f;
    [SerializeField, Min(0f)] private float magicEntranceDistance = 180f;
    [Tooltip("Radial origin in the Magic Motion Root's local space, below the settled slots.")]
    [SerializeField] private Vector2 magicRadialCenter = new Vector2(0f, -620f);
    [SerializeField, Min(0f)] private float magicSlotTravel = 440f;
    [Tooltip("Fraction of the intro between the first and last occupied slots starting to move.")]
    [SerializeField, Range(0f, 0.8f)] private float magicSlotStagger = 0.38f;

    // Never capture a fixed destination from a slot. Curve layout, JuicyMotion and card
    // content can update it, so apply just our visual displacement and don't own their tweens.
    private sealed class OffsetTrack
    {
        public readonly RectTransform Root;
        private Vector2 rest;
        private Vector2 written;
        private bool displaced;

        public OffsetTrack(RectTransform root) => Root = root;

        public void Clear()
        {
            if (!displaced) return;
            // An owner may have written a new absolute position since our last pose.
            if (Root && Root.anchoredPosition.Equals(written)) Root.anchoredPosition = rest;
            displaced = false;
        }

        public void Apply(Vector2 offset)
        {
            Clear();
            if (!Root || offset == Vector2.zero) return;
            rest = Root.anchoredPosition;
            Root.anchoredPosition = written = rest + offset;
            displaced = true;
        }
    }

    private sealed class PanelTrack
    {
        public readonly OffsetTrack Motion;
        public readonly CanvasGroup Group;
        public readonly float AuthoredAlpha;
        public readonly BattleInput Input;
        public readonly Vector2 BattleOffset;
        public readonly ExplorationReveal Exploration;
        public readonly bool HoverDetails;
        public float HoverProgress;

        public PanelTrack(Panel panel, CanvasGroup group, Vector2 offset, ExplorationReveal exploration, bool hoverDetails)
        {
            Motion = new OffsetTrack(panel.motionRoot);
            Group = group;
            AuthoredAlpha = group.alpha;
            Input = panel.battleInput;
            BattleOffset = offset;
            Exploration = exploration;
            HoverDetails = hoverDetails;
        }

        public void Show(float opacity, bool raycasts, bool interactable)
        {
            if (!Group) return;
            Group.alpha = AuthoredAlpha * opacity;
            Group.blocksRaycasts = raycasts && Group.alpha > 0.001f;
            Group.interactable = interactable && Group.blocksRaycasts;
        }

        public void SetActive(bool active)
        {
            if (Motion.Root && Motion.Root.gameObject.activeSelf != active)
                Motion.Root.gameObject.SetActive(active);
        }
    }

    private sealed class SlotTrack
    {
        public readonly OffsetTrack Motion;
        private CanvasGroup group;
        private float authoredAlpha;
        private bool fading;

        public SlotTrack(RectTransform slot) => Motion = new OffsetTrack(slot);

        public void Fade(float opacity)
        {
            if (!Motion.Root) return;
            if (!fading)
            {
                if (!group && !Motion.Root.TryGetComponent(out group))
                    group = Motion.Root.gameObject.AddComponent<CanvasGroup>();
                authoredAlpha = group.alpha;
                fading = true;
            }
            group.alpha = authoredAlpha * opacity;
        }

        public void Restore()
        {
            Motion.Clear();
            if (!fading) return;
            if (group) group.alpha = authoredAlpha;
            fading = false;
        }
    }

    private readonly List<PanelTrack> panels = new List<PanelTrack>();
    private readonly List<SlotTrack> slots = new List<SlotTrack>();
    private PanelTrack magicTrack;
    private readonly List<MagicSlotClickHandler> magicHandlers = new List<MagicSlotClickHandler>();
    private readonly List<MagicSlotClickHandler> mutedMagicHandlers = new List<MagicSlotClickHandler>();
    private bool magicCanHover;
    private Mode mode;
    private bool initialized;
    private bool explorationHoverBlocked;
    private float enterStarted;
    private float enterSeconds;

    /// <summary>True once all bound roots have arrived and their battle input policy is restored.</summary>
    public bool IsBattleEntranceComplete => mode == Mode.Battle;

    private void Awake()
    {
        if (!initialized) SetExploring();
    }

    /// <summary>Reset the entrance and allow PC hover to reveal exploration panels again.</summary>
    public void SetExploring()
    {
        Initialize();
        SetHiddenMode(Mode.Exploring);
    }

    /// <summary>Close the HUD and ignore hover, e.g. during a camera transfer.</summary>
    public void SetSuspended()
    {
        Initialize();
        SetHiddenMode(Mode.Suspended);
    }

    /// <summary>Temporarily suppress exploration hover without changing battle or transfer state.</summary>
    public void SetExplorationHoverBlocked(bool blocked)
    {
        if (explorationHoverBlocked == blocked) return;
        explorationHoverBlocked = blocked;
        if (blocked && initialized && mode == Mode.Exploring) ResetExplorationPanels();
    }

    /// <summary>Enable the real HUD for population but keep it invisible and noninteractive.</summary>
    public void PrepareBattle()
    {
        Initialize();
        mode = Mode.PreparingBattle;
        magicCanHover = false;
        RestoreMagicHandlers();
        ClearOffsets();
        RestoreSlots();
        // Gate the entire collection before activating anything that might run OnEnable.
        foreach (PanelTrack panel in panels) panel.Show(0f, false, false);
        foreach (PanelTrack panel in panels) panel.SetActive(true);
    }

    /// <summary>Activate the real HUD synchronously, then reveal it over seconds of unscaled time.</summary>
    public void EnterBattle(float seconds)
    {
        if (mode != Mode.PreparingBattle) PrepareBattle();
        enterSeconds = seconds;
        enterStarted = Time.unscaledTime;
        mode = Mode.EnteringBattle;
        // PrepareBattle already gates the roots. Compose the first visible pose only
        // in LateUpdate so battle setup never reads our start displacement as layout.
        if (!(seconds > 0f) || float.IsInfinity(seconds)) FinishBattleEntrance();
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        var used = new HashSet<RectTransform>();
        RegisterPanel(hand, Vector2.down * handEntranceDistance, used, ExplorationReveal.Hand);
        RegisterPanel(play, Vector2.down * playEntranceDistance, used, ExplorationReveal.Play);
        RegisterPanel(enemy, Vector2.up * enemyEntranceDistance, used);
        magicTrack = RegisterPanel(magic, Vector2.down * magicEntranceDistance, used, ExplorationReveal.Magic, true);
        if (battleOnlyPanels != null)
        {
            foreach (BattleOnlyPanel extra in battleOnlyPanels)
                if (extra != null)
                    RegisterPanel(extra.panel, extra.enterOffset, used, extra.explorationReveal, extra.explorationHoverDetails);
        }
        if (magicTrack == null || magicSlots == null) return;
        foreach (RectTransform slot in magicSlots)
        {
            if (!slot) continue;
            if (!slot.IsChildOf(magicTrack.Motion.Root) || !used.Add(slot))
            {
                Debug.LogWarning("Second-floor HUD magic slots must be distinct descendants of the Magic Motion Root.", slot);
                continue;
            }
            slots.Add(new SlotTrack(slot));
        }
    }

    private PanelTrack RegisterPanel(Panel panel, Vector2 offset, HashSet<RectTransform> used,
        ExplorationReveal exploration = ExplorationReveal.None, bool hoverDetails = false)
    {
        if (panel == null || !panel.motionRoot) return null;
        RectTransform root = panel.motionRoot;
        CanvasGroup group = panel.visibility ? panel.visibility : root.GetComponent<CanvasGroup>();
        if (!group || group.transform != root || transform.IsChildOf(root) || !used.Add(root))
        {
            Debug.LogWarning("Second-floor HUD panels need distinct motion roots with CanvasGroups on those same objects. Keep the motion component outside those roots.", root);
            return null;
        }
        var track = new PanelTrack(panel, group, offset, exploration, hoverDetails);
        panels.Add(track);
        return track;
    }

    private void SetHiddenMode(Mode hiddenMode)
    {
        mode = hiddenMode;
        magicCanHover = false;
        ClearOffsets();
        RestoreSlots();
        foreach (PanelTrack panel in panels)
        {
            ResetHover(panel);
            // Keep exploration roots enabled for the shared HandSystem to bind their contents.
            panel.SetActive(panel.Exploration != ExplorationReveal.None);
        }
    }

    private void ResetExplorationPanels()
    {
        magicCanHover = false;
        foreach (PanelTrack panel in panels)
            if (panel.Exploration != ExplorationReveal.None) ResetHover(panel);
    }

    private static void ResetHover(PanelTrack panel)
    {
        panel.HoverProgress = 0f;
        panel.Motion.Clear();
        panel.Show(0f, false, false);
    }

    // Restore layout coordinates before normal Update work; then compose the pose after
    // layout/normal DOTween Update. A later absolute write is accepted at the next Clear.
    private void Update()
    {
        if (mode == Mode.Exploring || mode == Mode.EnteringBattle) ClearOffsets();
        if (mode != Mode.Exploring) return;
        bool hasPointer = TryGetExplorationPointer(out Vector2 point);
        bool handHovered = hasPointer && handHoverRegion.Contains(point);
        bool playHovered = hasPointer && playHoverRegion.Contains(point);
        bool magicHovered = hasPointer && magicHoverRegion.Contains(point);
        foreach (PanelTrack panel in panels)
        {
            switch (panel.Exploration)
            {
                case ExplorationReveal.Hand: AdvanceHover(panel, handHovered); break;
                case ExplorationReveal.Play: AdvanceHover(panel, playHovered); break;
                case ExplorationReveal.Magic: AdvanceHover(panel, magicHovered); break;
            }
        }
    }

    private bool TryGetExplorationPointer(out Vector2 point)
    {
        point = default;
        if (explorationHoverBlocked || !Application.isFocused || Application.isMobilePlatform ||
            !Input.mousePresent || Input.touchCount > 0 || Cursor.lockState == CursorLockMode.Locked ||
            Screen.width < 1 || Screen.height < 1) return false;
        Vector3 pixel = Input.mousePosition;
        point = new Vector2(pixel.x / Screen.width, pixel.y / Screen.height);
        return point.x >= 0f && point.x < 1f && point.y >= 0f && point.y <= 0.5f;
    }

    private void AdvanceHover(PanelTrack panel, bool hover)
    {
        float target = hover ? 1f : 0f;
        panel.HoverProgress = hoverSeconds <= 0f ? target :
            Mathf.MoveTowards(panel.HoverProgress, target, Time.unscaledDeltaTime / hoverSeconds);
    }

    private void LateUpdate()
    {
        if (mode == Mode.Exploring)
        {
            foreach (PanelTrack panel in panels)
                if (panel.Exploration != ExplorationReveal.None) ApplyHover(panel);
        }
        else if (mode == Mode.EnteringBattle)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - enterStarted) / enterSeconds);
            if (t >= 1f) FinishBattleEntrance();
            else ApplyBattleEntrance(t);
        }
    }

    private void ApplyHover(PanelTrack panel)
    {
        float t = panel.HoverProgress;
        float eased = EaseOutCubic(t);
        // Hand / play themselves are ornamental. Revealed Magic and optional detail
        // panels keep their existing raycast-based hover without enabling child Selectables.
        bool allowHover = panel.HoverDetails && t >= 0.95f;
        if (panel == magicTrack && magicCanHover != allowHover)
        {
            if (allowHover) MuteMagicActionHandlers();
            magicCanHover = allowHover;
        }
        panel.Show(eased, allowHover, false);
        if (t > 0f) panel.Motion.Apply(Vector2.down * (hoverSlideDistance * (1f - eased)));
    }

    private void MuteMagicActionHandlers()
    {
        if (!magicTrack.Motion.Root) return;
        // This custom click/drag handler does not consult CanvasGroup.interactable.
        // Keep MagicItemView and its detail trigger enabled for their existing hover.
        magicTrack.Motion.Root.GetComponentsInChildren(true, magicHandlers);
        foreach (MagicSlotClickHandler handler in magicHandlers)
        {
            if (!handler || !handler.enabled) continue;
            handler.enabled = false;
            mutedMagicHandlers.Add(handler);
        }
        magicHandlers.Clear();
    }

    private void RestoreMagicHandlers()
    {
        foreach (MagicSlotClickHandler handler in mutedMagicHandlers)
            if (handler) handler.enabled = true;
        mutedMagicHandlers.Clear();
    }

    private void ApplyBattleEntrance(float t)
    {
        float travel = 1f - EaseOutCubic(t);
        float shown = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.38f));
        foreach (PanelTrack panel in panels)
        {
            panel.Motion.Apply(panel.BattleOffset * travel);
            panel.Show(shown, false, false);
        }
        if (magicTrack == null || !magicTrack.Motion.Root) return;
        RectTransform magicRoot = magicTrack.Motion.Root;
        int count = 0;
        foreach (SlotTrack slot in slots)
            if (slot.Motion.Root && slot.Motion.Root.gameObject.activeInHierarchy && slot.Motion.Root.IsChildOf(magicRoot)) count++;

        int rank = 0;
        foreach (SlotTrack slot in slots)
        {
            RectTransform root = slot.Motion.Root;
            if (!root || !root.gameObject.activeInHierarchy || !root.IsChildOf(magicRoot))
            {
                slot.Restore();
                continue;
            }

            float delay = count > 1 ? (float)rank / (count - 1) * magicSlotStagger : 0f;
            rank++;
            float localTime = Mathf.Clamp01((t - delay) / (1f - delay));
            slot.Motion.Clear();
            // Start inside the arc and expand along each slot's radial line. Translate
            // only; the curve layout's current angle and scale stay authoritative.
            Vector2 radial = (Vector2)magicRoot.InverseTransformPoint(root.position) - magicRadialCenter;
            Vector2 inward = radial.sqrMagnitude > 0.001f ? -radial.normalized : Vector2.down;
            Vector3 worldOffset = magicRoot.TransformVector(inward * magicSlotTravel);
            Vector2 localOffset = root.parent.InverseTransformVector(worldOffset);
            slot.Motion.Apply(localOffset * (1f - EaseOutElastic(localTime)));
            slot.Fade(Mathf.Clamp01(localTime / 0.18f));
        }
    }

    private void FinishBattleEntrance()
    {
        mode = Mode.Battle;
        ClearOffsets();
        RestoreSlots();
        foreach (PanelTrack panel in panels)
            panel.Show(1f, panel.Input != BattleInput.None, panel.Input == BattleInput.Full);
    }

    private void ClearOffsets()
    {
        foreach (PanelTrack panel in panels) panel.Motion.Clear();
        foreach (SlotTrack slot in slots) slot.Motion.Clear();
    }

    private void RestoreSlots()
    {
        foreach (SlotTrack slot in slots) slot.Restore();
    }

    private void OnDisable()
    {
        ClearOffsets();
        RestoreSlots();
        if (mode == Mode.Exploring) ResetExplorationPanels();
        else if (mode == Mode.EnteringBattle)
            foreach (PanelTrack panel in panels) panel.Show(0f, false, false);
        RestoreMagicHandlers();
    }

    private static float EaseOutCubic(float t)
    {
        float remaining = 1f - t;
        return 1f - remaining * remaining * remaining;
    }

    private static float EaseOutElastic(float t)
    {
        if (t <= 0f || t >= 1f) return t;
        return 1f + Mathf.Pow(2f, -9f * t) * Mathf.Sin((t - 0.09f) * (2f * Mathf.PI / 0.36f));
    }
}
