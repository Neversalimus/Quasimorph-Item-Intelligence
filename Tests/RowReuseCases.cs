namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Construct immutable presentation inputs without changing production visibility.
        private static BrowserLine ReuseRow(
            string left = "Left", string right = "Right",
            BrowserLineStyle style = BrowserLineStyle.Normal,
            BrowserLeftContentKind content = BrowserLeftContentKind.Text,
            BrowserAction action = default(BrowserAction), bool chipContext = false,
            string chip = "", BrowserChipStatus chipStatus = BrowserChipStatus.None,
            string faction = "", BrowserFactionRelation relation = BrowserFactionRelation.Neutral,
            BrowserRowKind kind = BrowserRowKind.Default, string reward = "",
            string unlock = "", string current = "", string state = "", string icon = "",
            BrowserTradeArrivalState arrival = BrowserTradeArrivalState.None)
        {
            return (BrowserLine)Activator.CreateInstance(typeof(BrowserLine),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { left, right, style, content, action, chipContext, chip,
                    chipStatus, faction, relation, kind, reward, unlock, current, state, icon, arrival },
                CultureInfo.InvariantCulture);
        }

        private static void RunRowReuseCases()
        {
            int previousTab = BrowserNavigation.Tab;
            bool previousReadability = EnhancedReadability;
            BrowserNavigation.SwitchTab((int)BrowserTabId.Overview);
            EnhancedReadability = false;
            BrowserRowRoots[0] = new GameObject();
            BrowserRowLeft[0] = new TMP_Text();
            BrowserRowRight[0] = new TMP_Text();
            BrowserRowButtons[0] = new Button();
            InvalidateBrowserRowRenderCache();
            BrowserLine baseline = ReuseRow();
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "uncaptured row cannot be reused");
            CheckReadability(!IsBrowserRowRenderReuseSafe(null), "null row cannot be reused");

            // Only these text-only Overview rows can skip rendering. A newly introduced
            // row kind must keep rendering until its inputs have been audited explicitly.
            foreach (BrowserRowKind kind in Enum.GetValues(typeof(BrowserRowKind)))
            {
                bool allowed = new HashSet<BrowserRowKind> {
                    BrowserRowKind.Default, BrowserRowKind.FullNote, BrowserRowKind.FullSection,
                    BrowserRowKind.OverviewCombatHeader, BrowserRowKind.BaronLootHeader,
                    BrowserRowKind.BaronLootRow }.Contains(kind);
                BrowserLine row = ReuseRow(kind: kind);
                CaptureBrowserRowRenderStamp(0, row, "en");
                CheckReadability(CanReuseBrowserRowRender(0, row, "en") == allowed,
                    "reuse policy for row kind " + kind);
            }
            CheckReadability(!IsBrowserRowRenderReuseSafe(ReuseRow(kind: (BrowserRowKind)999)),
                "unrecognized row kind fails closed");
            foreach (BrowserTabId tab in Enum.GetValues(typeof(BrowserTabId)))
            {
                BrowserNavigation.SwitchTab((int)tab);
                CaptureBrowserRowRenderStamp(0, baseline, "en");
                CheckReadability(CanReuseBrowserRowRender(0, baseline, "en") == (tab == BrowserTabId.Overview),
                    "reuse policy for tab " + tab);
            }
            BrowserNavigation.SwitchTab((int)BrowserTabId.Overview);
            foreach (BrowserLeftContentKind content in Enum.GetValues(typeof(BrowserLeftContentKind)))
                CheckReadability(IsBrowserRowRenderReuseSafe(ReuseRow(content: content)) ==
                    (content == BrowserLeftContentKind.Text), "asynchronous content is not reusable: " + content);
            foreach (BrowserChipStatus status in Enum.GetValues(typeof(BrowserChipStatus)))
                CheckReadability(IsBrowserRowRenderReuseSafe(ReuseRow(chipStatus: status)) ==
                    (status == BrowserChipStatus.None), "chip state is not reusable: " + status);
            foreach (BrowserLine row in new BrowserLine[] {
                ReuseRow(action: BrowserAction.OpenItem("powder")), ReuseRow(chipContext: true),
                ReuseRow(chip: "chip"), ReuseRow(faction: "faction"), ReuseRow(icon: "container") })
            {
                CaptureBrowserRowRenderStamp(0, row, "en");
                CheckReadability(!CanReuseBrowserRowRender(0, row, "en"), "dynamic row fails closed even with a matching stamp");
            }

            CaptureBrowserRowRenderStamp(0, baseline, "en");
            BeginBrowserRowRenderReusePass();
            for (int i = 0; i < 1000; i++)
                if (!CanReuseBrowserRowRender(0, baseline, "EN"))
                    throw new Exception("Readability: unchanged row stopped reusing its render");
            CheckReadability(_browserRowReuseHitsThisPass == 1000, "reuse counts only successful hits");
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "ja"), "language switch rejects stale row text");
            EnhancedReadability = true;
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "readability change rejects stale row styling");
            EnhancedReadability = false;
            CheckReadability(CanReuseBrowserRowRender(0, ReuseRow(), "en"), "equivalent new row can reuse rendering");
            CheckReadability(!CanReuseBrowserRowRender(0, ReuseRow(left: "Changed"), "en"), "changed row text redraws");

            // Exercise each captured input independently, including ones that are also
            // excluded by policy. This protects the stamp if a safe row is extended later.
            foreach (BrowserLine changed in new BrowserLine[] {
                ReuseRow(left: "left"), ReuseRow(right: "right"), ReuseRow(style: BrowserLineStyle.Note),
                ReuseRow(content: BrowserLeftContentKind.Item), ReuseRow(action: BrowserAction.OpenItem("powder")),
                ReuseRow(chipContext: true), ReuseRow(chip: "chip"), ReuseRow(chipStatus: BrowserChipStatus.Locked),
                ReuseRow(faction: "faction"), ReuseRow(relation: BrowserFactionRelation.Hostile),
                ReuseRow(kind: BrowserRowKind.FullNote), ReuseRow(reward: "reward"), ReuseRow(unlock: "unlock"),
                ReuseRow(current: "current"), ReuseRow(state: "state"), ReuseRow(icon: "icon"),
                ReuseRow(arrival: BrowserTradeArrivalState.ComparisonUnavailable) })
                CheckReadability(!BrowserRowRenderStamps[0].Matches(changed, "en"), "changed stamp input requires rendering");
            BrowserNavigation.SwitchTab((int)BrowserTabId.Magnum);
            CheckReadability(!BrowserRowRenderStamps[0].Matches(baseline, "en"), "stamp includes current tab");
            BrowserNavigation.SwitchTab((int)BrowserTabId.Overview);
            CheckReadability(!BrowserRowRenderStamps[0].Matches(null, "en"), "stamp rejects missing row");
            CaptureBrowserRowRenderStamp(0, baseline, null);
            CheckReadability(CanReuseBrowserRowRender(0, baseline, ""), "missing language normalizes consistently");

            foreach (int slot in new int[] { -1, BrowserRowCapacity, int.MaxValue })
            {
                CaptureBrowserRowRenderStamp(slot, baseline, "en");
                RestoreCachedBrowserRowBindings(slot, baseline);
                CheckReadability(!CanReuseBrowserRowRender(slot, baseline, "en"), "invalid slot fails closed");
            }
            CaptureBrowserRowRenderStamp(0, baseline, "en");
            CaptureBrowserRowRenderStamp(0, null, "ja");
            CheckReadability(CanReuseBrowserRowRender(0, baseline, "en"), "null capture does not replace a valid stamp");
            GameObject root = BrowserRowRoots[0]; BrowserRowRoots[0] = null;
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "missing row root requires rendering");
            BrowserRowRoots[0] = root;
            TMP_Text left = BrowserRowLeft[0]; BrowserRowLeft[0] = null;
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "missing left label requires rendering");
            BrowserRowLeft[0] = left;
            TMP_Text right = BrowserRowRight[0]; BrowserRowRight[0] = null;
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "missing right label requires rendering");
            BrowserRowRight[0] = right;

            root.activeSelf = false; left.raycastTarget = true; BrowserRowButtons[0].interactable = true;
            RestoreCachedBrowserRowBindings(0, baseline);
            CheckReadability(root.activeSelf && !left.raycastTarget && !BrowserRowButtons[0].interactable,
                "reused pooled row restores visibility and clears old interaction");
            RestoreCachedBrowserRowBindings(0, ReuseRow(action: BrowserAction.OpenItem("powder")));
            CheckReadability(BrowserRowButtons[0].interactable, "binding helper respects current action");
            RestoreCachedBrowserRowBindings(0, null);
            CheckReadability(BrowserRowButtons[0].interactable, "null binding update leaves existing row alone");
            InvalidateBrowserRowRenderCache();
            CheckReadability(!CanReuseBrowserRowRender(0, baseline, "en"), "invalidation clears captured rendering");
            BeginBrowserRowRenderReusePass();
            CheckReadability(_browserRowReuseHitsThisPass == 0, "new pass resets hit counter");
            BrowserNavigation.SwitchTab(previousTab); EnhancedReadability = previousReadability;
        }
    }
}
