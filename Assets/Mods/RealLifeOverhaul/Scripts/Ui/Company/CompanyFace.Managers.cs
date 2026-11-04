using System;

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
            float ty = y;
            if (m.Filters != null && m.Filters.Length > 1)
            {
                ty += UiDraw.Chips(s, at, x, ty, treeW, m.Filters, m.Filters.Length, Filter) + UiTokens.S3;
            }
            UiOrgTreeModel tree = TreeModel(m, v, w, lay);
            short list = UiOrgTree.Lay(s, at.InRegion(RegionList), tree, x, ty, treeW, bottom - ty, keys);
            lay.Offset[CompanyLayout.SlotTree] = tree.Offset;
            lay.List[CompanyLayout.SlotTree] = list;
            lay.Clip[CompanyLayout.SlotTree] = list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;
            float px = x + treeW + gap, pw = cw - treeW - gap;
            UiDraw.JoinColumns(s, x, x + treeW, x + cw, -1);
            if (m.Profile == null)
            {
                UiDraw.EmptyState(s, at, px, y, pw, bottom - y, UiIcon.IdBadge, w.T("reallife_co_mgr_none_title", "Nobody picked"),
                    string.IsNullOrEmpty(m.Empty) ? w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.")
                        : m.Empty, null, -1, keys);
                return;
            }
            ProfilePage(s, at.InRegion(RegionMain), m.Profile, v, w, lay, px, y, pw, bottom, keys);
        }

        private static UiOrgTreeModel TreeModel(CompanyManagersFacts m, CompanyView v, CompanyWords w, CompanyLayout lay)
        {
            int n = m.Tree.Length;
            var folded = new bool[n];
            int foldable = 0, foldedCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (m.Tree[i].HasKids)
                {
                    foldable++;
                    folded[i] = v.FoldedNodes.Contains(m.TreeIds[i]);
                    foldedCount += folded[i] ? 1 : 0;
                }
            }
            int[] shown = UiOrgTree.Visible(m.Tree, folded);
            var nodes = new UiOrgNode[shown.Length];
            var ids = new string[shown.Length];
            int chosen = -1;
            for (int k = 0; k < shown.Length; k++)
            {
                nodes[k] = m.Tree[shown[k]];
                nodes[k].Open = !folded[shown[k]];
                ids[k] = m.TreeIds[shown[k]];
                chosen = ids[k] == m.Chosen && m.Tree[shown[k]].Kind != UiOrgKind.Caption ? k : chosen;
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

        // The profile of the one chosen: their head, then the decisions and limits in a column and, beside it, the question waiting, how they are doing, the log and the crew.
        private static void ProfilePage(UiScene s, UiAt at, CompanyProfile p, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float pw, float bottom, IUiKeys keys)
        {
            var pm = new UiProfileModel
            {
                Name = p.Name, Hue = p.Hue, Ring = p.Ring, Role = p.Role, RoleIcon = UiIcon.IdBadge, Status = p.Status, StatusTone = p.StatusTone, FactLabels = p.FactLabels, FactValues = p.FactValues
            };
            float cy = UiAppWindow.Profile(s, at, pm, null, x, y, pw, keys);
            cy -= UiTokens.S2;
            float gap = UiTokens.S6;
            float c1w = UiDraw.SnapDown((pw - gap) * 0.56f), c2w = pw - gap - c1w, c2x = x + c1w + gap;
            // The page's own buttons run along its foot across both columns: replace, stand down, hand back, the dashboard.
            if (p.Buttons.Length > 0)
            {
                float footY = bottom - UiTokens.Control;
                Buttons(s, at, lay, v, p.Buttons, x, footY, pw, keys, true);
                bottom = footY - UiTokens.S3;
            }
            // The left column: what they decide, and their limits.
            float ly = cy;
            if (p.Tasks.Length > 0)
            {
                ly = Section(s, at, lay, x, ly, c1w, p.TasksTitle, p.TasksHint);
                for (int i = 0; i < p.Tasks.Length && ly + TaskRow <= bottom; i++)
                {
                    ly = TaskLine(s, at, lay, ref p.Tasks[i], x, ly, c1w);
                }
                ly += UiTokens.S2;
            }
            if (p.Limits.Length > 0 || p.RouteOptions.Length > 0)
            {
                CompanyButton? all = string.IsNullOrEmpty(p.LimitsAll.Label) ? (CompanyButton?)null : p.LimitsAll;
                ly = Section(s, at, lay, x, ly, c1w, p.LimitsTitle, null, all);
                for (int i = 0; i < p.Limits.Length && ly + SwitchRow <= bottom; i++)
                {
                    CompanyLimit l = p.Limits[i];
                    var b = new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Stepper, Text = l.Title, Sub = l.Sub, Value = l.Value, CanLess = l.CanLess, CanMore = l.CanMore, Disabled = l.Disabled, Why = l.Why, Act = l.Less,
                            Act2 = l.More,
                        Act3 = l.Typed
                    };
                    ly = Block(s, at, lay, v, w, b, x, ly, c1w, bottom, keys);
                }
                if (p.RouteOptions.Length > 0 && ly + SwitchRow <= bottom)
                {
                    var b = new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = p.RouteTitle, Sub = p.RouteSub, Options = p.RouteOptions, Chosen = p.RouteChosen, Acts = p.RouteActs };
                    ly = Block(s, at, lay, v, w, b, x, ly, c1w, bottom, keys);
                }
            }
            for (int i = 0; i < p.More.Length && ly < bottom; i++)
            {
                ly = Block(s, at, lay, v, w, p.More[i], x, ly, c1w, bottom, keys);
            }
            // The right column: the question waiting, how they are doing, their log, their crew.
            at = at.InRegion(RegionSide);
            float ry = cy;
            if (!string.IsNullOrEmpty(p.WaitingText))
            {
                ry += WaitingBanner(s, at, lay, v, p, c2x, ry, c2w, keys) + UiTokens.S4;
            }
            if (p.GradeLabels.Length > 0)
            {
                ry = Section(s, at, lay, c2x, ry, c2w, p.GradesTitle);
                float label = UiDraw.LabelColumn(c2w, p.GradeLabels);
                for (int i = 0; i < p.GradeLabels.Length && i < p.GradeValues.Length && ry + UiTokens.PairRow <= bottom; i++)
                {
                    ry += UiDraw.GradeBar(s, at, c2x, ry, c2w, p.GradeLabels[i], p.GradeValues[i], label) + UiTokens.S1;
                }
                ry += UiTokens.S3;
            }
            if (p.Log.Length > 0 && bottom - ry >= UiTokens.PairRow + DoneRow * 2f)
            {
                CompanyButton? all = string.IsNullOrEmpty(p.LogAll) ? (CompanyButton?)null : new CompanyButton { Label = p.LogAll, Act = p.LogOpen };
                ry = Section(s, at, lay, c2x, ry, c2w, p.LogTitle, null, all);
                int reserve = p.Crew.Length > 0 ? (int)(UiTokens.PairRow + ListRow * 2f + UiTokens.S4) : 0;
                int fit = (int)Math.Max(1f, Math.Floor((bottom - ry - reserve) / DoneRow));
                for (int i = 0; i < Math.Min(p.Log.Length, fit); i++)
                {
                    ry += DoneLine(s, at, lay, ref p.Log[i], c2x, ry, c2w, DoneRow);
                }
                ry += UiTokens.S3;
            }
            if (p.Crew.Length > 0 && bottom - ry >= UiTokens.PairRow + ListRow)
            {
                ry = Section(s, at, lay, c2x, ry, c2w, p.CrewTitle, p.CrewCount);
                int fit = (int)Math.Max(1f, Math.Floor((bottom - ry + ListGap) / (ListRow + ListGap)));
                for (int i = 0; i < Math.Min(p.Crew.Length, fit); i++)
                {
                    ref CompanyItem it = ref p.Crew[i];
                    int face = UiDraw.Box(s, at, c2x, ry, c2w, ListRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                    RowFace(s, at.In(face), c2x, ry, c2w, ListRow, it.Title, it.Sub, it.Figure, it.Note, it.Chip, it.ChipTone, it.FigureTone, it.Icon, it.Tint, true, it.Hue, it.Ring, it.Grade,
                        false, false);
                    if (it.Press && it.Act.Do != CompanyDo.None)
                    {
                        int index = lay.Add(new CompanyButton { Label = it.Title, Act = it.Act });
                        UiDraw.Node(s, at, c2x, ry, c2w, ListRow, Item, index, face);
                    }
                    ry += ListRow + ListGap;
                }
            }
            if (!string.IsNullOrEmpty(p.Note) && ry + UiTokens.S8 <= bottom)
            {
                Sentence(s, at, p.Note, c2x, ry, c2w, UiRole.Text2, 3, UiStyle.Small);
            }
        }

        // A decision a manager owns: its glyph on a tile, what it is and its limit in a line, the one who decides now and the switch.
        private static float TaskLine(UiScene s, UiAt at, CompanyLayout lay, ref CompanyTask t, float x, float y, float w)
        {
            int index = lay.Add(new CompanyButton { Label = t.Title, Act = t.Act, Disabled = t.Disabled, Why = t.Why });
            float switchW = Math.Min(UiDraw.SnapDown(w * 0.4f), SwitchWidth(t.Who));
            float textW = w - switchW - UiTokens.S3;
            UiAt inside = at;
            float size = UiTokens.ControlSmall;
            int tile = UiDraw.Tile(s, inside, x, y + Mid(TaskRow, size), size, UiRole.None, false);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, inside.In(tile), x + (size - g) / 2f, y + Mid(TaskRow, size) + (size - g) / 2f, g, t.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + size + UiTokens.S3, tw = textW - size - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((TaskRow - th - sh) / 2f);
            UiDraw.TextMiddle(s, inside, tx, top, th, tw, UiStyle.Title, t.Disabled ? UiRole.Text2 : UiRole.Text, t.Title ?? string.Empty, UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, tx, top + th, sh, tw, UiStyle.Small, UiRole.Text2, t.Line ?? string.Empty, UiFlags.Truncate);
            UiDraw.Switch(s, at, x + w - switchW, y + Mid(TaskRow, UiTokens.Control), switchW, t.On, t.Who, Act, index, t.Disabled);
            return y + TaskRow;
        }

        // The question waiting for the owner on a manager's page: what it is and the one button that answers it.
        private static float WaitingBanner(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyProfile p, float x, float y, float w, IUiKeys keys)
        {
            float pad = UiTokens.S4;
            CompanyButton b = p.Waiting;
            float bw = Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys));
            float tw = w - pad * 2f - bw - UiTokens.S3;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float lh = string.IsNullOrEmpty(p.WaitingLine) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(p.WaitingLine, UiStyle.Small, tw, 3));
            float h = Math.Max(UiTokens.Control, th + (lh > 0f ? UiTokens.S1 + lh : 0f)) + pad * 2f;
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(box);
            UiDraw.TextMiddle(s, inside, x + pad, y + pad, th, tw, UiStyle.Title, UiRole.Text, p.WaitingText, UiFlags.Truncate);
            if (lh > 0f)
            {
                UiDraw.Text(s, inside, x + pad, y + pad + th + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, p.WaitingLine, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            Button(s, inside, lay, v, ref b, x + w - pad - bw, y + Mid(h, UiTokens.Control), bw, keys);
            return h;
        }
    }
}
