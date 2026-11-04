using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float TaskRow = 44f;

        // The managers: who decides what as a tree at the left, and at the right the page of the one chosen (the decisions they own as switches, their limits, how they
        // are doing, their log and their crew). A sub page (the inbox, the policy centre, a dashboard, the seats) is a list and a card in place of both.
        private static void ManagersPage(UiScene s, UiAt at, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            CompanyManagersFacts m = f.Managers;
            if (m.PartOff)
            {
                OffPage(s, at, w, m.OffTitle, m.OffBody, x, y, cw, bottom, keys);
                return;
            }
            if (v.Sub[CompanyView.Managers] != CompanyView.SubMain)
            {
                if (m.Sub.List.Rows.Length == 0 && string.IsNullOrEmpty(m.Sub.List.Title) && m.Sub.Card != null)
                {
                    CardPage(s, at, m.Sub.Card, v, w, lay, x, y, cw, bottom, keys);   // a page of one card (a kind's limits)
                    return;
                }
                ListPage(s, at, m.Sub, v, w, lay, x, y, cw, bottom, keys);
                return;
            }
            float gap = UiTokens.S6;
            float treeW = UiDraw.SnapDown(Math.Min(cw * 0.27f, 420f));
            float ty = y + FilterChips(s, at.InRegion(RegionList), m, v, w, x, y, treeW);
            UiOrgTreeModel tree = TreeModel(m, v, w, lay);
            short list = UiOrgTree.Lay(s, at.InRegion(RegionList), tree, x, ty, treeW, bottom - ty, keys);
            lay.Offset[CompanyLayout.SlotTree] = tree.Offset;
            lay.List[CompanyLayout.SlotTree] = list;
            lay.Clip[CompanyLayout.SlotTree] = list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;
            float px = x + treeW + gap, pw = cw - treeW - gap;
            UiDraw.JoinColumns(s, x, x + treeW, x + cw, -1);
            if (m.Profile == null)
            {
                OffPage(s, at.InRegion(RegionMain), w, w.T("reallife_co_mgr_none_title", "Nobody picked"),
                    string.IsNullOrEmpty(m.Empty) ? w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.") : m.Empty,
                    px, y, pw, bottom, keys);
                return;
            }
            ProfilePage(s, at.InRegion(RegionMain), m.Profile, v, w, lay, px, y, pw, bottom, keys);
        }

        // ------------------------------------------------------------------------------------------------- the tree and its two filters

        // The filters over the tree: the places it shows (every place, those waiting for you, the open posts) and the district (every one, or one of those the places stand in). Each is
        // one chip that turns to the next on a press, so that sixty four places in a dozen districts take two targets and not a row of chips. Returns the height they take.
        private static float FilterChips(UiScene s, UiAt at, CompanyManagersFacts m, CompanyView v, CompanyWords w, float x, float y, float treeW)
        {
            int places = 0;
            for (int i = 0; i < m.TreeIds.Length; i++)
            {
                places += IsPlaceRow(m.TreeIds[i]) ? 1 : 0;
            }
            int districts = m.Districts == null ? 0 : m.Districts.Length;
            bool byState = places > 1, byDistrict = districts > 1;
            if (!byState && !byDistrict)
            {
                return 0f;
            }
            int state = v.State, district = Math.Max(0, Math.Min(districts, v.District));
            float each = byState && byDistrict ? UiDraw.SnapDown((treeW - UiTokens.S2) / 2f) : treeW, cx = x;
            if (byState)
            {
                string label = state == CompanyView.StateNeeds ? w.T("reallife_mgr_filter_needs", "Needs you") : state == CompanyView.StatePosts ? w.T("reallife_co_tree_open_posts", "Open posts")
                    : w.T("reallife_co_flt_places", "All places");
                int next = (state + 1) % CompanyView.StateCount + district * CompanyView.DistrictStep;
                FilterChip(s, at, cx, y, each, label, state != CompanyView.StateAll, next);
                cx += each + UiTokens.S2;
            }
            if (byDistrict)
            {
                string label = district == 0 ? w.T("reallife_tab_all_districts", "All districts") : m.Districts[district - 1];
                int next = state + (district + 1) % (districts + 1) * CompanyView.DistrictStep;
                FilterChip(s, at, cx, y, each, label, district != 0, next);
            }
            return UiTokens.Chip + UiTokens.S3;
        }

        // A chip that names the filter now in force (filled while it cuts the tree) and, pressed, sets the filter to the number given.
        private static void FilterChip(UiScene s, UiAt at, float x, float y, float most, string label, bool active, int arg)
        {
            float w = Math.Min(most, UiDraw.ButtonWidth(UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, null));
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.Chip, active ? UiRole.Accent : UiRole.AccentSoft, UiRole.None, UiTokens.RadiusMedium, active ? UiFlags.Selected : UiFlags.None);
            UiDraw.TextMiddle(s, at.In(face), x + UiTokens.S4, y, UiTokens.Chip, Math.Max(0f, w - UiTokens.S4 * 2f), UiStyle.Button, active ? UiRole.OnAccent : UiRole.AccentText, label,
                UiFlags.Centre | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, UiTokens.Chip, Filter, arg, face);
        }

        // A row of a place of the company (a manager's, a region's, an open post's), as against the owner, the executives, the crew and the captions.
        private static bool IsPlaceRow(string id) =>
            id.StartsWith("mgr:", StringComparison.Ordinal) || id.StartsWith("reg:", StringComparison.Ordinal) || id.StartsWith("post:", StringComparison.Ordinal);

        // Which rows of the full tree stand under the two filters: the owner always; an executive unless only the open posts are asked for; a place by its district and by whether it waits
        // for the owner or is an open post; a manager's crew with the manager; a caption while any row of its group stands.
        private static bool[] Kept(CompanyManagersFacts m, CompanyView v)
        {
            int n = m.Tree.Length, state = v.State;
            string district = v.District > 0 && v.District <= m.Districts.Length ? m.Districts[v.District - 1] : null;
            var keep = new bool[n];
            bool manager = true;
            for (int i = 0; i < n; i++)
            {
                string id = m.TreeIds[i];
                if (m.Tree[i].Kind == UiOrgKind.Caption)
                {
                    continue;
                }
                if (id.StartsWith("crew:", StringComparison.Ordinal) || id.StartsWith("more:", StringComparison.Ordinal))
                {
                    keep[i] = manager;
                    continue;
                }
                bool post = id.StartsWith("post:", StringComparison.Ordinal), place = IsPlaceRow(id);
                bool k;
                if (id == "own")
                {
                    k = true;
                }
                else if (!place)
                {
                    k = id.StartsWith("seat:", StringComparison.Ordinal) ? state != CompanyView.StateNeeds && district == null : state != CompanyView.StatePosts;
                }
                else
                {
                    string where = i < m.TreeDistricts.Length ? m.TreeDistricts[i] : null;
                    bool needs = i < m.TreeNeeds.Length && m.TreeNeeds[i];
                    k = (district == null || string.Equals(where, district, StringComparison.Ordinal))
                        && (state == CompanyView.StateAll || (state == CompanyView.StateNeeds && needs && !post) || (state == CompanyView.StatePosts && post));
                }
                keep[i] = k;
                if (id.StartsWith("mgr:", StringComparison.Ordinal))
                {
                    manager = k;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (m.Tree[i].Kind != UiOrgKind.Caption)
                {
                    continue;
                }
                for (int j = i + 1; j < n && m.Tree[j].Kind != UiOrgKind.Caption && !keep[i]; j++)
                {
                    keep[i] = keep[j];
                }
            }
            return keep;
        }

        private static UiOrgTreeModel TreeModel(CompanyManagersFacts m, CompanyView v, CompanyWords w, CompanyLayout lay)
        {
            bool[] keep = Kept(m, v);
            var rows = new List<UiOrgNode>(m.Tree.Length + 1);
            var rowIds = new List<string>(m.Tree.Length + 1);
            bool placesLeft = false;
            for (int i = 0; i < m.Tree.Length; i++)
            {
                if (keep[i])
                {
                    rows.Add(m.Tree[i]);
                    rowIds.Add(m.TreeIds[i]);
                    placesLeft |= IsPlaceRow(m.TreeIds[i]);
                }
            }
            if (v.Filter != 0 && !placesLeft)
            {
                rows.Add(new UiOrgNode { Kind = UiOrgKind.More, Level = 0, Name = w.T("reallife_co_flt_none", "No place matches") });
                rowIds.Add("none");
            }
            int n = rows.Count;
            var folded = new bool[n];
            int foldable = 0, foldedCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (rows[i].HasKids)
                {
                    foldable++;
                    folded[i] = v.IsFolded(rowIds[i]);
                    foldedCount += folded[i] ? 1 : 0;
                }
            }
            UiOrgNode[] full = rows.ToArray();
            int[] shown = UiOrgTree.Visible(full, folded);
            var nodes = new UiOrgNode[shown.Length];
            var ids = new string[shown.Length];
            int chosen = -1;
            for (int k = 0; k < shown.Length; k++)
            {
                nodes[k] = full[shown[k]];
                nodes[k].Open = !folded[shown[k]];
                ids[k] = rowIds[shown[k]];
                chosen = ids[k] == m.Chosen && full[shown[k]].Kind != UiOrgKind.Caption ? k : chosen;
            }
            lay.TreeIds = ids;
            string action = foldable == 0 ? null : foldedCount == foldable ? w.T("reallife_co_expand_all", "Expand all") : w.T("reallife_co_collapse_all", "Collapse all");
            float offset = lay.Offset[CompanyLayout.SlotTree];
            int reveal = lay.Reveal[CompanyLayout.SlotTree];
            lay.Reveal[CompanyLayout.SlotTree] = -1;
            return new UiOrgTreeModel
            {
                Title = string.IsNullOrEmpty(m.TreeTitle) ? w.T("reallife_co_who_decides", "Who decides what") : m.TreeTitle, Action = action, Nodes = nodes, Chosen = chosen,
                FootLabel = m.TreeFoot, FootIcon = UiIcon.Sliders, Offset = offset, Reveal = reveal, FoldTargets = false
            };
        }
    }
}
