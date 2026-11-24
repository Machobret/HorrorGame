using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.UI
{
    internal enum ReporterVerb
    {
        Equip, Unequip, Use, Combine, Examine, Drop, Refill, Read, Pin, Map, ArchiveTab, DocumentsTab, Group,
        PreviousPage, NextPage, ExitReader
    }

    internal readonly struct ReporterAction
    {
        public readonly ReporterVerb Verb;
        public readonly string Label;

        public ReporterAction(ReporterVerb verb, string label)
        {
            Verb = verb;
            Label = label;
        }
    }

    internal sealed class ReporterNode
    {
        public string Id { get; }
        public string Title { get; }
        public string Detail { get; }
        public Sprite Image { get; }
        public float? StackCount { get; }
        public float? LoadedAmmo { get; }
        public float? Charge { get; }
        public IReadOnlyList<ReporterAction> Actions { get; }

        public ReporterNode(string id, string title, string detail, Sprite image, float? stackCount, params ReporterAction[] actions)
            : this(id, title, detail, image, stackCount, null, null, actions)
        {
        }

        public ReporterNode(string id, string title, string detail, Sprite image, float? stackCount,
            float? loadedAmmo, float? charge, params ReporterAction[] actions)
        {
            Id = id;
            Title = title;
            Detail = detail;
            Image = image;
            StackCount = stackCount;
            LoadedAmmo = loadedAmmo;
            Charge = charge;
            Actions = actions;
        }
    }

    internal sealed class ReporterDocument
    {
        public readonly ReporterNode Node;
        public readonly string Section;
        public readonly IReadOnlyList<string> Pages;
        public readonly bool Archived;
        public readonly bool Pinned;

        public ReporterDocument(ReporterNode node, string section, string[] pages, bool archived, bool pinned)
        {
            Node = node;
            Section = section;
            Pages = pages;
            Archived = archived;
            Pinned = pinned;
        }
    }

    internal enum ReporterOutcome { Failed, Applied, OpenMap, Examine, Close, Reload }
    internal enum ReporterCondition { Unknown, Healthy, Wounded, Critical }

    internal sealed class ReporterMenuSnapshot
    {
        public readonly IReadOnlyList<ReporterNode> Equipment;
        public readonly IReadOnlyList<ReporterNode> Items;
        public readonly IReadOnlyList<ReporterDocument> Documents;
        public readonly ReporterNode Tablet;
        public readonly string CharacterName;
        public readonly Sprite Portrait;
        public readonly ReporterCondition Condition;
        public readonly int Capacity;
        public readonly int UsedSlots;

        public ReporterMenuSnapshot(ReporterNode[] equipment, ReporterNode[] items, ReporterDocument[] documents,
            ReporterNode tablet, string characterName, Sprite portrait, ReporterCondition condition, int capacity, int usedSlots)
        {
            Equipment = equipment;
            Items = items;
            Documents = documents;
            Tablet = tablet;
            CharacterName = characterName;
            Portrait = portrait;
            Condition = condition;
            Capacity = capacity;
            UsedSlots = usedSlots;
        }
    }

    internal sealed class ReporterMenuBridge
    {
        sealed class ItemSource
        {
            public InventoryEntry Entry;
            public ItemData Item;
            public EquipmentSlot Slot;
        }

        sealed class ItemIdentity
        {
            public int Value;
        }

        readonly Inventory inventory;
        readonly ConditionalWeakTable<InventoryEntry, ItemIdentity> itemIds = new();
        readonly Dictionary<string, ItemSource> items = new();
        readonly Dictionary<string, DocumentData> documents = new();
        Health health;
        int nextItemId;

        public ReporterMenuSnapshot Snapshot { get; private set; }
        public int LedgerRevision => inventory.DocumentLedger.Revision;
        public ReporterCondition CurrentCondition
        {
            get
            {
                if (!health || health.Max <= 0) return ReporterCondition.Unknown;
                float ratio = health.Normalized;
                if (ratio <= 0.33f) return ReporterCondition.Critical;
                return ratio < 0.66f ? ReporterCondition.Wounded : ReporterCondition.Healthy;
            }
        }
        public bool IsCurrent(int version) => GameManager.Exists && GameManager.Instance.CharacterState != null
            && GameManager.Instance.Inventory == inventory && inventory.MenuSessionVersion == version;

        public ReporterMenuBridge(Inventory inventory) => this.inventory = inventory;

        public void Refresh()
        {
            var manager = GameManager.Instance;
            health = manager.Player ? manager.Player.GetComponent<Health>() : null;

            items.Clear();
            documents.Clear();
            var carried = new Dictionary<InventoryEntry, ReporterNode>();
            var inventoryItems = new List<ReporterNode>();
            foreach (var entry in inventory.Items.Where(entry => entry?.Item).Distinct())
            {
                var node = ItemNode(entry, inventory.GetOccupyingEquipmentSlot(entry));
                carried.Add(entry, node);
                inventoryItems.Add(node);
            }
            var equipment = new List<ReporterNode>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.None) continue;
                var entry = inventory.GetEquipped(slot);
                if (entry?.Item && carried.TryGetValue(entry, out var carriedNode))
                {
                    equipment.Add(carriedNode);
                    continue;
                }

                var node = ItemNode(entry, slot);
                equipment.Add(node);
                if (node.Id != null) inventoryItems.Add(node);
            }
            var ledger = inventory.Documents.Where(document => document && !string.IsNullOrEmpty(document.UniqueId))
                .Select(DocumentNode).OrderBy(document => document.Section, StringComparer.CurrentCulture)
                .ThenBy(document => document.Node.Title, StringComparer.CurrentCulture)
                .ThenBy(document => document.Node.Id, StringComparer.Ordinal).ToArray();
            var tablet = Chapter1.Chapter1MapTutorial.CanOpenMap
                ? new ReporterNode("tablet", "", "", null, null, new ReporterAction(ReporterVerb.Map, "Open map"))
                : null;
            Snapshot = new ReporterMenuSnapshot(equipment.ToArray(), inventoryItems.ToArray(), ledger, tablet, manager.Character.Name,
                manager.Character.Portrait, CurrentCondition, inventory.Capacity, inventory.UsedSlots);
        }

        ReporterNode ItemNode(InventoryEntry entry, EquipmentSlot slot)
        {
            string slotName = slot == EquipmentSlot.Primary ? "Weapon" : slot == EquipmentSlot.Secondary ? "Tool" : slot.ToString();
            if (entry?.Item == null)
                return new ReporterNode(null, GameText.Translate(slotName), GameText.Translate("Empty"), null, null);

            ItemData item = entry.Item;
            string id = $"item:{itemIds.GetValue(entry, _ => new ItemIdentity { Value = ++nextItemId }).Value}:{item.UniqueId}";
            items.Add(id, new ItemSource { Entry = entry, Item = item, Slot = slot });

            var actions = new List<ReporterAction>();
            if (CanEquip(entry)) actions.Add(new ReporterAction(ReporterVerb.Equip, "Equip"));
            if (slot != EquipmentSlot.None)
            {
                if (CanUnequip(entry, slot))
                    actions.Add(new ReporterAction(ReporterVerb.Unequip, "Unequip"));
                if (FindRefill(entry) != null)
                    actions.Add(new ReporterAction(ReporterVerb.Refill, "Reload batteries"));
            }
            if (item.InventoryAction.HasFlag(InventoryMainAction.Use)) actions.Add(new ReporterAction(ReporterVerb.Use, "Use"));
            if (item.Flags.HasFlag(ItemFlags.Combinable)) actions.Add(new ReporterAction(ReporterVerb.Combine, "Combine"));
            if (item.Flags.HasFlag(ItemFlags.Examinable)) actions.Add(new ReporterAction(ReporterVerb.Examine, "Examine"));
            if (item.Flags.HasFlag(ItemFlags.Droppable)) actions.Add(new ReporterAction(ReporterVerb.Drop, "Drop"));

            float? stackCount = item.Flags.HasFlag(ItemFlags.Stackable) ? entry.Count : null;
            float? loadedAmmo = item is ReloadableWeaponData ? entry.SecondaryCount : null;
            float? charge = item.Flags.HasFlag(ItemFlags.Depletable)
                ? Mathf.Clamp01(entry.Status) : null;
            return new ReporterNode(id, item.Name, item.Description, item.Image, stackCount, loadedAmmo, charge, actions.ToArray());
        }

        ReporterDocument DocumentNode(DocumentData document)
        {
            string id = $"document:{document.UniqueId}";
            documents.Add(id, document);
            bool archived = inventory.DocumentLedger.IsArchived(document.UniqueId);
            bool pinned = inventory.DocumentLedger.IsPinned(document.UniqueId);
            var pages = TranscriptPages(document).ToArray();

            var actions = new List<ReporterAction>();
            if (!archived) actions.Add(new ReporterAction(ReporterVerb.Pin, pinned ? "Unpin" : "Pin"));
            if (pages.Length > 0) actions.Add(new ReporterAction(ReporterVerb.Read, "Read"));
            string section = document.LedgerSection;
            var node = new ReporterNode(id, document.Name, "", DocumentArtwork(document), null, actions.ToArray());
            return new ReporterDocument(node, string.IsNullOrWhiteSpace(section) ? "Collected documents" : section, pages, archived, pinned);
        }

        internal static IEnumerable<string> TranscriptPages(DocumentData document) =>
            document.Pages?.Where(page => page != null).Select(page => (string)page.Text)
                .Where(page => !string.IsNullOrWhiteSpace(page)) ?? Enumerable.Empty<string>();

        internal static Sprite DocumentArtwork(DocumentData document)
        {
            Sprite pageArtwork = document.Pages?.Select(page => page?.Artwork ? page.Artwork : (Sprite)page?.Image)
                .FirstOrDefault(image => image);
            return pageArtwork ? pageArtwork : (Sprite)document.Image;
        }

        public ReporterOutcome Execute(string id, ReporterVerb verb, string otherId, out ItemData examined)
        {
            examined = null;
            if (GameManager.Instance.Inventory != inventory) return ReporterOutcome.Failed;
            if (verb == ReporterVerb.Map)
                return Chapter1.Chapter1MapTutorial.CanOpenMap ? ReporterOutcome.OpenMap : ReporterOutcome.Failed;
            if (verb == ReporterVerb.Read || verb == ReporterVerb.Pin)
                return ExecuteDocument(id, verb);
            if (!TryItem(id, out var source)) return ReporterOutcome.Failed;

            var entry = source.Entry;
            var item = entry.Item;
            switch (verb)
            {
                case ReporterVerb.Equip when CanEquip(entry):
                    inventory.Equip(entry);
                    var equipped = inventory.GetEquipped(((EquipableItemData)item).Slot);
                    if (equipped?.Item != item) return ReporterOutcome.Failed;
                    if (equipped != entry && entry.Item != item) TransferItemIdentity(entry, equipped);
                    return ReporterOutcome.Applied;
                case ReporterVerb.Unequip:
                    var slot = inventory.GetOccupyingEquipmentSlot(entry);
                    if (!CanUnequip(entry, slot)) return ReporterOutcome.Failed;
                    var carriedBefore = inventory.Items.Contains(entry) ? null : inventory.Items
                        .Where(value => value?.Item == item).Distinct().ToDictionary(value => value, value => value.Count);
                    inventory.Unequip(slot);
                    if (inventory.GetEquipped(slot) != null) return ReporterOutcome.Failed;
                    if (carriedBefore != null)
                    {
                        var returned = inventory.Items.FirstOrDefault(value => value?.Item == item &&
                            (!carriedBefore.TryGetValue(value, out float count) || value.Count > count));
                        if (returned != null) TransferItemIdentity(entry, returned);
                    }
                    return ReporterOutcome.Applied;
                case ReporterVerb.Use when item.InventoryAction.HasFlag(InventoryMainAction.Use):
                    return inventory.Use(entry) ? ReporterOutcome.Close : ReporterOutcome.Failed;
                case ReporterVerb.Examine when item.Flags.HasFlag(ItemFlags.Examinable):
                    examined = item;
                    return ReporterOutcome.Examine;
                case ReporterVerb.Drop when item.Flags.HasFlag(ItemFlags.Droppable):
                    if (item.Flags.HasFlag(ItemFlags.CreatePickupOnDrop))
                    {
                        var player = GameManager.Instance.Player;
                        var dropper = player ? player.GetComponentInChildren<PickupDropper>() : null;
                        if (!dropper) return ReporterOutcome.Failed;
                        dropper.Drop(entry);
                    }
                    inventory.Drop(entry);
                    return ReporterOutcome.Close;
                case ReporterVerb.Combine when item.Flags.HasFlag(ItemFlags.Combinable):
                    return TryItem(otherId, out var other) ? Combine(entry, other.Entry) : ReporterOutcome.Failed;
                case ReporterVerb.Refill:
                    var refill = FindRefill(entry);
                    return refill != null ? Combine(entry, refill) : ReporterOutcome.Failed;
                default:
                    return ReporterOutcome.Failed;
            }
        }

        ReporterOutcome ExecuteDocument(string id, ReporterVerb verb)
        {
            if (id == null || !documents.TryGetValue(id, out var document) || !inventory.Documents.Contains(document))
                return ReporterOutcome.Failed;
            if (verb == ReporterVerb.Pin)
                return inventory.DocumentLedger.TogglePin(document.UniqueId) ? ReporterOutcome.Applied : ReporterOutcome.Failed;
            if (document.Pages?.Any(page => page != null && !string.IsNullOrWhiteSpace((string)page.Text)) != true)
                return ReporterOutcome.Failed;
            inventory.ReadDocuments.Add(document);
            return ReporterOutcome.Applied;
        }

        ReporterOutcome Combine(InventoryEntry first, InventoryEntry second)
        {
            if (first == second) return ReporterOutcome.Failed;
            bool reload = inventory.CanRequestWeaponReload(first, second);
            inventory.Combine(first, second, out bool combined);
            if (!combined) return ReporterOutcome.Failed;
            if (!reload) return ReporterOutcome.Applied;
            inventory.RequestInventoryReopenAfterReload(first.Item is ReloadableWeaponData ? first : second);
            return ReporterOutcome.Reload;
        }

        bool TryItem(string id, out ItemSource source)
        {
            source = null;
            if (id == null || !items.TryGetValue(id, out source) || source.Entry.Item != source.Item) return false;
            return source.Slot == EquipmentSlot.None
                ? inventory.Items.Contains(source.Entry)
                : inventory.GetEquipped(source.Slot) == source.Entry;
        }

        void TransferItemIdentity(InventoryEntry from, InventoryEntry to)
        {
            if (!itemIds.TryGetValue(from, out var identity)) return;
            itemIds.Remove(from);
            itemIds.Remove(to);
            itemIds.Add(to, identity);
        }

        bool CanEquip(InventoryEntry entry)
        {
            if (entry.Item is not EquipableItemData item || item.Slot == EquipmentSlot.None
                || !item.InventoryAction.HasFlag(InventoryMainAction.Equip) || inventory.GetOccupyingEquipmentSlot(entry) != EquipmentSlot.None)
                return false;
            var current = inventory.GetEquipped(item.Slot);
            return current == null || CanUnequip(current, item.Slot)
                || item.MoveOutOfInventoryOnEquip && item.Slot != EquipmentSlot.Secondary
                    && current.Item && Mathf.Max(1, item.InventorySlots) + inventory.FreeSlots >= current.Item.InventorySlots;
        }

        bool CanUnequip(InventoryEntry entry, EquipmentSlot slot) =>
            slot != EquipmentSlot.None && entry.Item is EquipableItemData item
            && (!item.MoveOutOfInventoryOnEquip || slot == EquipmentSlot.Secondary || inventory.CanAdd(item, entry.Count));

        InventoryEntry FindRefill(InventoryEntry entry)
        {
            if (entry.Status >= 1 || !entry.Item.Flags.HasFlag(ItemFlags.Depletable)) return null;
            var recipes = GameManager.Instance.InventorySetup.Combinations;
            if (recipes == null) return null;
            foreach (var recipe in recipes)
            {
                if (recipe is not InventoryCombinationReplenishStatus refill) continue;
                var match = inventory.Items.FirstOrDefault(candidate => candidate?.Item && candidate != entry
                    && (refill.CanCombine(entry, candidate) || refill.CanCombine(candidate, entry)));
                if (match != null) return match;
            }
            return null;
        }
    }
}
