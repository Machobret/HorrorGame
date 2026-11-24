using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HorrorEngine;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.UI
{
    internal sealed class ReporterMenu
    {
        enum ReporterShortcut { None, Enter, Q, E, R }

        sealed class Session
        {
            public readonly int Version;
            public readonly ReporterMenuBridge Data;
            public readonly ReporterFocus Focus = new();
            public readonly HashSet<string> Collapsed = new();
            public readonly float[] TabScroll = { 1, 1 };
            public bool Archive;
            public string Reading;
            public int Page;
            public float ReadingScroll = 1;
            public float InventoryScroll = 1;
            public string CombineSource;
            public ReporterVerb? FocusedAction;

            public Session(Inventory inventory)
            {
                Version = inventory.MenuSessionVersion;
                Data = new ReporterMenuBridge(inventory);
            }
        }

        readonly UIInventory host;
        readonly UIContext context;
        readonly IUIInput input;
        readonly ReporterMenuView view;
        readonly ConditionalWeakTable<Inventory, Session> sessions = new();
        Session session;
        string feedback;
        float feedbackUntil;
        Vector2 heldDirection;
        float nextMove;
        int openedFrame;
        int ledgerRevision;
        bool covered;

        ReporterMenuSnapshot Data => session.Data.Snapshot;
        string Selected => session.Focus.Id;
        ReporterNode SelectedNode => GetNode(Selected);
        ReporterDocument Reading => GetDocument(session.Reading);

        public ReporterMenu(UIInventory host, UIContext context, IUIInput input, Sprite tabletIcon, ReporterMenuArt art)
        {
            this.host = host;
            this.context = context;
            this.input = input;
            // Built under the existing inventory canvas so its sorting, scaler and raycaster are reused.
            view = new ReporterMenuView(host.transform, tabletIcon, art,
                () => MouseReader(ReporterVerb.PreviousPage), () => MouseReader(ReporterVerb.NextPage),
                () => MouseReader(ReporterVerb.ExitReader));
        }

        public void Open(bool showLedger)
        {
            var inventory = GameManager.Instance.Inventory;
            session = sessions.GetValue(inventory, value => new Session(value));
            if (session.Version != inventory.MenuSessionVersion)
            {
                sessions.Remove(inventory);
                session = sessions.GetValue(inventory, value => new Session(value));
            }
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            openedFrame = Time.frameCount;
            heldDirection = Cardinal(input.GetPrimaryAxis());
            nextMove = Time.unscaledTime + 0.36f;
            SetFeedback("");
            Rebuild();
            if (showLedger)
            {
                session.Reading = null;
                session.CombineSource = null;
                session.FocusedAction = null;
                session.Focus.Select(session.Focus.Preferred(view.FocusTargets, ReporterPanel.Ledger));
            }
            Render(reveal: showLedger);
        }

        public void Close()
        {
            if (session == null) return;
            RememberScroll();
            if (session.Reading != null) session.ReadingScroll = view.ReaderScroll;
        }

        public void SetCovered(bool value)
        {
            covered = value;
            view.Block(value || context.IsBlocked(), hidden: value);
        }

        public void Tick()
        {
            if (covered || session == null) return;
            bool blocked = context.IsBlocked();
            view.Block(blocked);
            if (blocked || openedFrame == Time.frameCount) return;
            if (!session.Data.IsCurrent(session.Version))
            {
                if (GameManager.Exists && GameManager.Instance.CharacterState != null) Open(false);
                else host.Hide();
                return;
            }
            if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime >= feedbackUntil) SetFeedback("");
            if (session.Data.LedgerRevision != ledgerRevision)
            {
                RememberScroll();
                if (Reading != null) session.ReadingScroll = view.ReaderScroll;
                Rebuild();
                Render();
            }
            view.RefreshInventoryLayout();
            view.SetCondition(session.Data.CurrentCondition);
            if (input.IsCancelDown())
            {
                Back();
                return;
            }

            ReporterShortcut shortcut = ReadShortcut();
            if (shortcut != ReporterShortcut.None)
            {
                UseShortcut(shortcut);
                return;
            }
            if (Reading != null)
            {
                // Keep the reader outside EventSystem navigation. Controller shoulders turn pages.
                if (input.IsPrevSubmapDown()) Turn(ReporterVerb.PreviousPage);
                else if (input.IsNextSubmapDown())
                    Turn(session.Page + 1 < Reading.Pages.Count ? ReporterVerb.NextPage : ReporterVerb.ExitReader);
                return;
            }
            if (input.IsConfirmDown())
            {
                UseShortcut(ReporterShortcut.Enter);
                return;
            }
            Vector2 direction = Cardinal(input.GetPrimaryAxis());
            if (direction == Vector2.zero)
            {
                heldDirection = Vector2.zero;
                return;
            }
            if (direction != heldDirection || Time.unscaledTime >= nextMove)
            {
                nextMove = Time.unscaledTime + (heldDirection == direction ? 0.12f : 0.36f);
                heldDirection = direction;
                Move(direction);
            }
        }

        static Vector2 Cardinal(Vector2 axis)
        {
            if (axis.sqrMagnitude < 0.25f) return Vector2.zero;
            return Mathf.Abs(axis.x) >= Mathf.Abs(axis.y)
                ? new Vector2(Mathf.Sign(axis.x), 0) : new Vector2(0, Mathf.Sign(axis.y));
        }

        static ReporterShortcut ReadShortcut()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) return ReporterShortcut.Q;
                if (keyboard.eKey.wasPressedThisFrame) return ReporterShortcut.E;
                if (keyboard.rKey.wasPressedThisFrame) return ReporterShortcut.R;
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) return ReporterShortcut.Enter;
            }
#endif
            return ReporterShortcut.None;
        }

        static ReporterShortcut Binding(ReporterPanel panel, ReporterVerb verb) => (panel, verb) switch
        {
            (ReporterPanel.Inventory, ReporterVerb.Refill) => ReporterShortcut.R,
            (ReporterPanel.Ledger, ReporterVerb.Pin) => ReporterShortcut.E,
            (ReporterPanel.Ledger, ReporterVerb.Read) => ReporterShortcut.R,
            (ReporterPanel.Ledger, ReporterVerb.ArchiveTab or ReporterVerb.DocumentsTab or ReporterVerb.Group)
                or (ReporterPanel.Inventory, ReporterVerb.Map) => ReporterShortcut.Enter,
            (ReporterPanel.Display, ReporterVerb.PreviousPage) => ReporterShortcut.Q,
            (ReporterPanel.Display, ReporterVerb.NextPage or ReporterVerb.ExitReader) => ReporterShortcut.E,
            _ => ReporterShortcut.None
        };

        void Move(Vector2 direction)
        {
            bool leavingActions = session.FocusedAction.HasValue && direction.x != 0;
            if (leavingActions)
            {
                session.FocusedAction = null;
            }
            else if (session.FocusedAction.HasValue)
            {
                var actions = ItemActions();
                int index = Array.FindIndex(actions, action => action.Verb == session.FocusedAction);
                index = Mathf.Clamp(index - (int)direction.y, 0, actions.Length - 1);
                session.FocusedAction = actions[index].Verb;
                Render();
                return;
            }

            var target = session.Focus.Move(view.FocusTargets, direction);
            bool moved = target != null && target.Id != Selected;
            if (moved) session.Focus.Select(target);
            if (moved || leavingActions) Render(reveal: moved);
        }

        void UseShortcut(ReporterShortcut shortcut)
        {
            if (Reading != null)
            {
                foreach (var action in ReaderActions())
                    if (Binding(ReporterPanel.Display, action.Verb) == shortcut) Turn(action.Verb);
                return;
            }
            bool select = shortcut is ReporterShortcut.E or ReporterShortcut.Enter;
            if (session.FocusedAction.HasValue)
            {
                if (select) Apply(session.FocusedAction.Value);
                return;
            }
            if (select && session.CombineSource != null && Data.Items.Any(node => node.Id == Selected))
            {
                if (CanChooseCombineTarget()) CompleteCombine();
                return;
            }
            foreach (var action in Actions())
            {
                var binding = Binding(session.Focus.Panel, action.Verb);
                if (binding != shortcut && !(select && binding == ReporterShortcut.Enter)) continue;
                Apply(action.Verb);
                return;
            }
            if (select)
            {
                var itemActions = ItemActions();
                if (itemActions.Length == 0) return;
                session.FocusedAction = itemActions[0].Verb;
                Render();
            }
        }

        void Back()
        {
            if (Reading != null)
            {
                host.Hide();
                return;
            }
            if (session.FocusedAction.HasValue)
                session.FocusedAction = null;
            else if (session.CombineSource != null)
                session.CombineSource = null;
            else
            {
                host.Hide();
                return;
            }
            Render();
        }

        void PointerSelect(string id)
        {
            if (context.IsBlocked() || Reading != null) return;
            var target = view.FocusTargets.FirstOrDefault(node => node.Id == id);
            if (target == null) return;
            bool confirmCombine = Selected == id && session.CombineSource != null;
            session.FocusedAction = null;
            session.Focus.Select(target);
            if (confirmCombine && CanChooseCombineTarget()) CompleteCombine();
            else
            {
                foreach (var action in Actions())
                {
                    if (Binding(target.Panel, action.Verb) != ReporterShortcut.Enter) continue;
                    Apply(action.Verb);
                    return;
                }
                Render(reveal: true);
            }
        }

        void PointerPin(string id)
        {
            if (context.IsBlocked() || Reading != null) return;
            var document = GetDocument(id);
            if (document == null || document.Archived) return;
            bool toggle = document.Pinned || Selected == id;
            PointerSelect(id);
            if (toggle && Selected == id) Apply(ReporterVerb.Pin);
        }

        void MouseReader(ReporterVerb verb)
        {
            if (!context.IsBlocked()) Turn(verb);
        }

        void Apply(ReporterVerb verb)
        {
            if (context.IsBlocked()) return;
            switch (verb)
            {
                case ReporterVerb.ArchiveTab:
                case ReporterVerb.DocumentsTab:
                    RememberScroll();
                    session.Archive = verb == ReporterVerb.ArchiveTab;
                    Rebuild();
                    Render();
                    return;
                case ReporterVerb.Group:
                    RememberScroll();
                    if (!session.Collapsed.Add(Selected)) session.Collapsed.Remove(Selected);
                    Rebuild();
                    Render(reveal: true);
                    return;
                case ReporterVerb.Combine:
                    session.CombineSource = Selected;
                    session.FocusedAction = null;
                    SetFeedback("");
                    Render();
                    return;
                default:
                    Execute(Selected, verb);
                    return;
            }
        }

        void CompleteCombine()
        {
            string source = session.CombineSource;
            session.CombineSource = null;
            Execute(source, ReporterVerb.Combine, Selected);
        }

        bool CanChooseCombineTarget() => session.CombineSource != null && Selected != session.CombineSource
            && session.Focus.Panel == ReporterPanel.Inventory && Data.Items.Any(node => node.Id == Selected);

        void Execute(string target, ReporterVerb verb, string other = null)
        {
            RememberScroll();
            var result = session.Data.Execute(target, verb, other, out ItemData examined);
            session.FocusedAction = null;
            if (result == ReporterOutcome.OpenMap)
            {
                host.OnMapCategory();
                return;
            }
            if (result == ReporterOutcome.Examine)
            {
                host.Hide();
                UIManager.Get<UIExamineItem>().Show(examined);
                return;
            }
            if (result == ReporterOutcome.Close || result == ReporterOutcome.Reload)
            {
                host.Hide();
                return;
            }
            Rebuild();
            if (result == ReporterOutcome.Failed)
                SetFeedback(verb == ReporterVerb.Combine ? "Cannot combine these items" : "Action unavailable");
            else if (verb == ReporterVerb.Read && GetDocument(target) != null)
            {
                session.Reading = target;
                session.Page = 0;
                session.ReadingScroll = 1;
            }
            else SetFeedback("");
            Render(reveal: true);
        }

        void Turn(ReporterVerb verb)
        {
            var document = Reading;
            if (document == null) return;
            if (verb == ReporterVerb.ExitReader)
            {
                session.Reading = null;
                Render(reveal: true);
                return;
            }
            int next = session.Page + (verb == ReporterVerb.NextPage ? 1 : -1);
            if (next < 0 || next >= document.Pages.Count) return;
            session.Page = next;
            session.ReadingScroll = 1;
            Render();
        }

        ReporterDocument GetDocument(string id) => id == null ? null : Data.Documents.FirstOrDefault(doc => doc.Node.Id == id);

        ReporterNode GetNode(string id)
        {
            if (id == null) return null;
            return Data.Items.FirstOrDefault(node => node.Id == id)
                ?? GetDocument(id)?.Node ?? (Data.Tablet?.Id == id ? Data.Tablet : null);
        }

        IReadOnlyList<ReporterAction> Actions()
        {
            if (Selected == "tab:archive")
                return new[] { new ReporterAction(ReporterVerb.ArchiveTab, "Open Archive") };
            if (Selected == "tab:documents")
                return new[] { new ReporterAction(ReporterVerb.DocumentsTab, "Open Documents") };
            if (Selected?.StartsWith("group:", StringComparison.Ordinal) == true)
                return new[] { new ReporterAction(ReporterVerb.Group, session.Collapsed.Contains(Selected) ? "Expand" : "Collapse") };
            return SelectedNode?.Actions ?? Array.Empty<ReporterAction>();
        }

        ReporterAction[] ItemActions() => Actions().Where(action => Binding(session.Focus.Panel, action.Verb) == ReporterShortcut.None).ToArray();

        IEnumerable<ReporterAction> ReaderActions()
        {
            if (session.Page > 0) yield return new ReporterAction(ReporterVerb.PreviousPage, "Previous page");
            if (session.Page + 1 < Reading.Pages.Count)
                yield return new ReporterAction(ReporterVerb.NextPage, "Next page");
            else yield return new ReporterAction(ReporterVerb.ExitReader, "Exit");
        }

        void RememberScroll()
        {
            session.InventoryScroll = view.InventoryScroll;
            session.TabScroll[session.Archive ? 1 : 0] = view.LedgerScroll;
        }

        void Rebuild()
        {
            session.Data.Refresh();
            ledgerRevision = session.Data.LedgerRevision;
            view.Build(Data, session.Archive, session.Collapsed, PointerSelect, PointerPin,
                session.InventoryScroll, session.TabScroll[session.Archive ? 1 : 0]);
            session.Focus.Select(session.Focus.Restore(view.FocusTargets));
            if (session.CombineSource != null && !Data.Items.Any(node => node.Id == session.CombineSource))
                session.CombineSource = null;
            if (Reading == null || Reading.Pages.Count == 0) session.Reading = null;
            if (session.FocusedAction.HasValue && !ItemActions().Any(action => action.Verb == session.FocusedAction))
                session.FocusedAction = null;
        }

        void Render(bool reveal = false)
        {
            if (Reading != null)
            {
                session.Page = Mathf.Clamp(session.Page, 0, Reading.Pages.Count - 1);
                string legend = Legend(ReporterPanel.Display, ReaderActions());
                view.ShowFocus(Selected, ReporterPanel.Display, legend, reveal: false);
                view.Read(Reading.Node, Reading.Pages[session.Page], session.Page, Reading.Pages.Count, session.ReadingScroll);
                return;
            }
            var itemActions = session.CombineSource == null ? ItemActions() : Array.Empty<ReporterAction>();
            int focused = Array.FindIndex(itemActions, action => action.Verb == session.FocusedAction);
            var panel = focused >= 0 ? ReporterPanel.Display : session.Focus.Panel;
            string hint = focused >= 0 ? GameText.Translate("E Select  /  A or D Move") : Legend(panel, Actions());
            if (session.CombineSource != null && panel == ReporterPanel.Inventory)
                hint = CanChooseCombineTarget() ? GameText.Translate("E Combine  /  Esc Cancel") : GameText.Translate("Choose a second item");
            else if (focused < 0 && itemActions.Length > 0)
                hint = string.IsNullOrEmpty(hint) ? GameText.Translate("E Actions") : $"{GameText.Translate("E Actions")}  ·  {hint}";
            view.ShowFocus(Selected, panel, hint, reveal);
            view.Preview(Selected == Data.Tablet?.Id ? null : SelectedNode, GetDocument(Selected) != null, itemActions, focused, Apply);
        }

        static string Legend(ReporterPanel panel, IEnumerable<ReporterAction> actions) => string.Join("  ·  ", actions
            .Select(action => (Key: Binding(panel, action.Verb), action.Label))
            .Where(action => action.Key != ReporterShortcut.None)
            .Select(action => $"{(action.Key == ReporterShortcut.Enter ? ReporterShortcut.E : action.Key)} {GameText.Translate(action.Label)}"));

        void SetFeedback(string value)
        {
            view.Feedback(value);
            feedback = value;
            feedbackUntil = Time.unscaledTime + 2.5f;
        }
    }
}
