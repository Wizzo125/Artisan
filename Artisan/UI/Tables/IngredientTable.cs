﻿using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using static Artisan.UI.Tables.IngredientTable;

namespace Artisan.UI.Tables
{
    internal class IngredientTable : Table<Ingredient>, IDisposable
    {
        private static float _nameColumnWidth = 0;
        private static float _requiredColumnWidth = 80;
        private static float _idColumnWidth = 80;
        private static float _inventoryColumnWidth = 80;
        private static float _retainerColumnWidth = 80;
        private static float _remainingColumnWidth = 100;
        private static float _canCraftColumnWidth = 100;
        private static float _craftableCountColumnWidth = 100;
        private static float _craftItemsColumnWidth = 0;
        private static float _itemCategoryColumnWidth = 0;
        private static float _gatherItemLocationColumWidth = 0;
        private static float _craftingJobsColumnWidth = 100;
        private static float _cheapestColumnWidth = 100;
        private static float _numberForSaleWidth = 100;

        public readonly IdColumn _idColumn = new() { Label = "ID" };
        public readonly NameColumn _nameColumn = new() { Label = "Item Name" };
        public readonly RequiredColumn _requiredColumn = new() { Label = "Required" };
        public readonly InventoryCountColumn _inventoryColumn = new() { Label = "Inventory" };
        public readonly RetainerCountColumn _retainerColumn = new() { Label = "Retainers" };
        public readonly RemaingCountColumn _remainingColumn = new() { Label = "Remaining Needed" };
        public readonly CraftableColumn _craftableColumn = new() { Label = "Sources" };
        public readonly CraftableCountColumn _craftableCountColumn = new() { Label = "Number Craftable" };
        public readonly CraftItemsColumn _craftItemsColumn = new() { Label = "Used to Craft" };
        public readonly ItemCategoryColumn _itemCategoryColumn = new() { Label = "Category" };
        public readonly GatherItemLocationColumn _gatherItemLocationColumn = new() { Label = "Gathered Zone" };
        public readonly CheapestServerColumn _cheapestServerColumn = new() { Label = "Optimal World For Buying" };
        public readonly NumberForSaleColumn _numberForSaleColumn = new() { Label = "Quantity For Sale (All Worlds)" };

        private static bool GatherBuddy =>
            DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var _, false, true);

        private static bool ItemVendor =>
            DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var _, false, true);

        private static bool MonsterLookup =>
            DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var _, false, true);

        private static bool Marketboard =>
            DalamudReflector.TryGetDalamudPlugin("Market board", out var _, false, true);

        private static bool Lifestream =>
    DalamudReflector.TryGetDalamudPlugin("Lifestream", out var _, false, true);

        private static unsafe void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);

        public List<Ingredient> ListItems;

        private bool CraftFiltered = false;

        public IngredientTable(List<Ingredient> ingredientList)
            : base("IngredientTable", ingredientList)
        {

            if (P.Config.DefaultHideInventoryColumn)        _inventoryColumn.Flags          |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideRetainerColumn)         _retainerColumn.Flags           |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideRemainingColumn)        _remainingColumn.Flags          |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftableColumn)        _craftableColumn.Flags          |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftableCountColumn)   _craftableCountColumn.Flags     |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCraftItemsColumn)       _craftItemsColumn.Flags         |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideCategoryColumn)         _itemCategoryColumn.Flags       |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideGatherLocationColumn)   _gatherItemLocationColumn.Flags |= ImGuiTableColumnFlags.DefaultHide;
            if (P.Config.DefaultHideIdColumn)               _idColumn.Flags                 |= ImGuiTableColumnFlags.DefaultHide;

            Headers.Add(_nameColumn);
            Headers.Add(_requiredColumn);
            Headers.Add(_inventoryColumn);
            if (RetainerInfo.ATools) Headers.Add(_retainerColumn);
            Headers.Add(_remainingColumn);
            Headers.Add(_craftableColumn);
            Headers.Add(_craftableCountColumn);
            Headers.Add(_craftItemsColumn);
            Headers.Add(_itemCategoryColumn);
            Headers.Add(_gatherItemLocationColumn);
            if (P.Config.UseUniversalis)
            {
                Headers.Add(_cheapestServerColumn);
                Headers.Add(_numberForSaleColumn);
            }
            Headers.Add(_idColumn);

            Sortable = true;
            ListItems = ingredientList;
            Flags |= ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable;

            _nameColumn.OnContextMenuRequest += OpenContextMenu;
            _remainingColumn.SourceList = ListItems;

            foreach (var item in Items)
            {
                item.OnRemainingChange += SetFilterDirty;
            }
        }

        private void SetFilterDirty(object? sender, bool e)
        {
            this.FilterDirty = true;
        }

        public void Dispose()
        {
            _nameColumn.OnContextMenuRequest -= OpenContextMenu;

            foreach (var item in Items)
            {
                item.OnRemainingChange -= SetFilterDirty;
            }
        }

        public sealed class NameColumn : ColumnString<Ingredient>
        {
            public NameColumn()
               => Flags |= ImGuiTableColumnFlags.NoHide;

            public override string ToName(Ingredient item)
            {
                return item.Data.Name.RawString;
            }

            public bool ShowColour = false;
            public bool ShowHQOnly = false;

            public override float Width => _nameColumnWidth * ImGuiHelpers.GlobalScale;

            public override void DrawColumn(Ingredient item, int _)
            {
                if (ShowColour)
                {
                    int invAmount = ShowHQOnly && item.CanBeCrafted ? item.InventoryHQ : item.Inventory;
                    int retainerAmount = ShowHQOnly && item.CanBeCrafted ? item.ReainterCountHQ : item.RetainerCount;

                    if (item.CanBeCrafted && retainerAmount + invAmount + item.TotalCraftable >= item.Required)
                    {
                        var color = ImGuiColors.TankBlue;
                        color.W -= 0.6f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    if (retainerAmount + invAmount >= item.Required)
                    {
                        var color = ImGuiColors.DalamudOrange;
                        color.W -= 0.6f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    if (invAmount >= item.Required)
                    {
                        var color = ImGuiColors.HealerGreen;
                        color.W -= 0.3f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }
                }

                ImGuiUtil.HoverIcon(item.Icon, Interface.LineIconSize);
                ImGui.SameLine();

                var selected = ImGui.Selectable(item.Data.Name.RawString);
                InvokeContextMenu(item);

                if (selected)
                {
                    ImGui.SetClipboardText(item.Data.Name.RawString);
                    Notify.Success("Name copied to clipboard");
                }

                if (ImGui.IsItemHovered())
                {
                    StringBuilder sb = new();
                    foreach (var usedin in item.UsedInCrafts)
                    {
                        var recipe = LuminaSheets.RecipeSheet[usedin];
                        var amountUsed = recipe.UnkData5.FirstOrDefault(x => x.ItemIngredient == item.Data.RowId).AmountIngredient * item.OriginList.Items.Count(x => x == recipe.RowId);

                        sb.Append($"{usedin.NameOfRecipe()} - {amountUsed}\r\n");
                    }
                    ImGui.BeginTooltip();
                    ImGui.Text($"Used in:\r\n{sb}");
                    ImGui.EndTooltip();
                }
            }
        }

        public sealed class RequiredColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _requiredColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Required.CompareTo(rhs.Required);

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGuiUtil.Center($"{ToName(item)}");
            }

            public override string ToName(Ingredient item)
            {
                return item.Required.ToString();
            }
        }

        public sealed class IdColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _idColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Data.RowId.CompareTo(rhs.Data.RowId);

            public override string ToName(Ingredient item)
            {
                return item.Data.RowId.ToString();
            }
        }

        public sealed class InventoryCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _inventoryColumnWidth;

            public bool HQOnlyCrafts = false;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Inventory.CompareTo(rhs.Inventory);

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGuiUtil.Center($"{ToName(item)}");
            }

            public unsafe override string ToName(Ingredient item)
            {
                if (!HQOnlyCrafts || !item.CanBeCrafted)
                return item.Inventory.ToString();

                int HQ = InventoryManager.Instance()->GetInventoryItemCount(item.Data.RowId, true, false, false);
                return HQ.ToString();
            }
        }

        public sealed class RetainerCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _retainerColumnWidth;

            public bool HQOnlyCrafts = false;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.RetainerCount.CompareTo(rhs.RetainerCount);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center($"{ToName(item)}");

            public override string ToName(Ingredient item)
            {
                if (!HQOnlyCrafts || !item.CanBeCrafted)
                    return item.RetainerCount.ToString();

                int retainerHQ = item.ReainterCountHQ;
                return retainerHQ.ToString();
            }
        }

        public sealed class CraftableCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftableCountColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.TotalCraftable.CompareTo(rhs.TotalCraftable);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center(ToName(item));


            public override string ToName(Ingredient item)
            {
                return item.Sources.Contains(1) ? item.TotalCraftable.ToString() : "N/A";
            }
        }

        public sealed class CraftItemsColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftItemsColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.UsedInCrafts.First().CompareTo(rhs.UsedInCrafts.First());

            public override string ToName(Ingredient item)
            {
                return string.Join(", ", item.UsedInCrafts.Select(x => x.NameOfRecipe()));
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGui.Text(ToName(item));   
            }

        }

        public sealed class CheapestServerColumn : ColumnString<Ingredient>
        {
            public override float Width => _cheapestColumnWidth;
            public Dictionary<uint, (string World, double Qty, double Cost)> CheapestListings = new();

            public override int Compare(Ingredient lhs, Ingredient rhs)
            {
                var lh = lhs.MarketboardData?.LowestWorld;
                var rh = rhs.MarketboardData?.LowestWorld;

                if (lh == null || rh == null)
                    return 0;

                return lh.CompareTo(rh);
            }

            public override string ToName(Ingredient item)
            {
                if (item.MarketboardData != null && !CheapestListings.ContainsKey(item.Data.RowId))
                {
                    double totalCost = 0;
                    double qty = 0;


                    double currentWorldCost = 0;
                    string currentWorld = "";
                    double currentWorldQty = 0;

                    foreach (var world in item.MarketboardData.AllListings.Select(x => x.World))
                    {
                        totalCost = 0;
                        qty = 0;

                        foreach (var listing in item.MarketboardData.AllListings.Where(x => x.World == world).OrderBy(x => x.TotalPrice))
                        {
                            if (qty >= item.Required) break;
                            qty += listing.Quantity;
                            totalCost += listing.TotalPrice;
                        }

                        if ((totalCost < currentWorldCost && qty >= item.Required) || currentWorldCost == 0 || (qty > currentWorldQty && qty < item.Required))
                        {
                            currentWorldCost = totalCost;
                            currentWorld = world;
                            currentWorldQty = qty;
                        }
                    }

                    CheapestListings.TryAdd(item.Data.RowId, new(currentWorld, currentWorldQty, currentWorldCost));

                    item.MarketboardData.LowestWorld = currentWorld;
                }

                if (CheapestListings.ContainsKey(item.Data.RowId))
                {
                    var listing = CheapestListings[item.Data.RowId];

                    return $"{listing.World} - Cost {listing.Cost.ToString("N0")}, Qty {listing.Qty}";

                }

                return "ERROR - No Listings (Possible Universalis Connection Issue)";
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGui.Text($"{ToName(item)}");

                if (Lifestream && CheapestListings.ContainsKey(item.Data.RowId))
                {
                    var server = CheapestListings[item.Data.RowId].World;
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"Click to travel to {server}.");
                        ImGui.EndTooltip();
                    }

                    if (ImGui.IsItemClicked())
                    {
                        Chat.Instance.SendMessage($"/li {server}");
                    }
                }
            }
        }

        public sealed class NumberForSaleColumn : ColumnString<Ingredient>
        {
            public override float Width => _numberForSaleWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
            {
                var lh = lhs.MarketboardData?.TotalQuantityOfUnits;
                var rh = rhs.MarketboardData?.TotalQuantityOfUnits;

                if (lh == null || rh == null)
                    return 0;

                return lh.Value.CompareTo(rh.Value);
            }

            public override string ToName(Ingredient item)
            {
                if (item.MarketboardData != null)
                {
                    var qty = item.MarketboardData.TotalQuantityOfUnits;
                    var listings = item.MarketboardData.TotalNumberOfListings;

                    return $"{listings.Value.ToString("N0")} listings - {qty.Value.ToString("N0")} total items";
                }
                return "";
            }

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGui.Text($"{ToName(item)}");
            }
        }


        public sealed class GatherItemLocationColumn : ItemFilterColumn
        {
            public GatherItemLocationColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.GatherZone, ItemFilter.NoGatherZone, ItemFilter.TimedNode, ItemFilter.NonTimedNode);
                SetNames("Gather Zone", "No Gather Zone", "Timed Node", "Non-Timed Node");

            }
            public override float Width
                => _gatherItemLocationColumWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.GatherZone.PlaceName.Value.Name.RawString.CompareTo(rhs.GatherZone.PlaceName.Value.Name.RawString);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(item.GatherZone.PlaceName.Value.Name.RawString);
            }

            public override bool FilterFunc(Ingredient item)
            {
                bool zone = item.GatherZone.RowId switch
                {
                    1 => FilterValue.HasFlag(ItemFilter.NoGatherZone),
                    _ => FilterValue.HasFlag(ItemFilter.GatherZone)
                };

                bool timed = item.TimedNode switch
                {
                    true => FilterValue.HasFlag(ItemFilter.TimedNode),
                    false => FilterValue.HasFlag(ItemFilter.NonTimedNode)
                };

                return zone & timed;
            }
        }

        public sealed class ItemCategoryColumn : ItemFilterColumn
        {
            public ItemCategoryColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.NonCrystals, ItemFilter.Crystals);
                SetNames("Non-Crystals", "Crystals");
            }


            public override float Width
                => _itemCategoryColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Category.CompareTo(rhs.Category);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(Svc.Data.Excel.GetSheet<ItemSearchCategory>().GetRow(item.Category).Name.RawString);
            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Category switch
                {
                    58 => FilterValue.HasFlag(ItemFilter.Crystals),
                    _ => FilterValue.HasFlag(ItemFilter.NonCrystals)
                };
            }
        }

        public class ItemFilterColumn : ColumnFlags<ItemFilter, Ingredient>
        {
            private ItemFilter[] FlagValues = Array.Empty<ItemFilter>();
            private string[] FlagNames = Array.Empty<string>();

            protected void SetFlags(params ItemFilter[] flags)
            {
                FlagValues = flags;
                AllFlags = FlagValues.Aggregate((f, g) => f | g);
            }

            protected void SetFlagsAndNames(params ItemFilter[] flags)
            {
                SetFlags(flags);
                SetNames(flags.Select(f => f.ToString()).ToArray());
            }

            protected void SetNames(params string[] names)
                => FlagNames = names;

            protected sealed override IReadOnlyList<ItemFilter> Values
                => FlagValues;

            protected sealed override string[] Names
                => FlagNames;

            public sealed override ItemFilter FilterValue
                => P.Config.ShowItemsV1;

            protected sealed override void SetValue(ItemFilter f, bool v)
            {
                var tmp = v ? FilterValue | f : FilterValue & ~f;
                if (tmp == FilterValue)
                    return;

                P.Config.ShowItemsV1 = tmp;
                P.Config.Save();
            }
        }

        public sealed class RemaingCountColumn : ItemFilterColumn
        {
            public RemaingCountColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.MissingItems, ItemFilter.NoMissingItems);
                SetNames("Missing Items", "No Missing Items");
            }

            public override float Width
                => _remainingColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Remaining.CompareTo(rhs.Remaining);

            public List<Ingredient> SourceList = new();

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGuiUtil.Center($"{item.Remaining}");

                if (ImGui.IsItemHovered())
                {
                    StringBuilder sb = new StringBuilder();
                    if (item.UsedInMaterialsListCount.Count > 0)
                    {
                        foreach (var i in item.UsedInMaterialsListCount.Where(x => x.Value > 0))
                        {
                            var owned = RetainerInfo.GetRetainerItemCount(LuminaSheets.RecipeSheet[i.Key].ItemResult.Row) + CraftingListUI.NumberOfIngredient(LuminaSheets.RecipeSheet[i.Key].ItemResult.Row);
                            if (SourceList.FindFirst(x => x.CraftedRecipe?.RowId == i.Key, out var ingredient))
                            {
                                sb.Append($"{i.Value} less is required due to having {(owned > ingredient.Required ? "at least " : "")}{Math.Min(ingredient.Required, owned)}x {i.Key.NameOfRecipe()}\r\n");
                            }
                        }

                        ImGuiUtil.HoverTooltip(sb.ToString().Trim());
                    }
                }

            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Remaining switch
                {
                    0 => FilterValue.HasFlag(ItemFilter.NoMissingItems),
                    _ => FilterValue.HasFlag(ItemFilter.MissingItems)
                };
            }
        }

        public sealed class CraftableColumn : ItemFilterColumn
        {
            public CraftableColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.Crafted, ItemFilter.Gathered, ItemFilter.Fishing, ItemFilter.Vendor, ItemFilter.MonsterDrop, ItemFilter.Unknown);
                SetNames("Crafted", "Gathered", "Fishing", "Vendor", "Monster Drop", "Unknown");
            }


            public override float Width
                => _canCraftColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => string.Join(", ", lhs.Sources).CompareTo(string.Join(", ", rhs.Sources));

            public override void DrawColumn(Ingredient item, int idx)
            {
                List<string> outputs = new();

                if (item.Sources.Contains(1)) outputs.Add("Crafted");
                if (item.Sources.Contains(2)) outputs.Add("Gathered");
                if (item.Sources.Contains(3)) outputs.Add("Fishing");
                if (item.Sources.Contains(4)) outputs.Add("Vendor");
                if (item.Sources.Contains(5)) outputs.Add("Monster Drop");
                if (item.Sources.Contains(-1)) outputs.Add("Unknown");

                ImGui.Text($"{string.Join(", ", outputs)}");
            }

            public override bool FilterFunc(Ingredient item)
            {
                if (item.Sources.Contains(1) && FilterValue.HasFlag(ItemFilter.Crafted)) return true;
                if (item.Sources.Contains(2) && FilterValue.HasFlag(ItemFilter.Gathered)) return true;
                if (item.Sources.Contains(3) && FilterValue.HasFlag(ItemFilter.Fishing)) return true;
                if (item.Sources.Contains(4) && FilterValue.HasFlag(ItemFilter.Vendor)) return true;
                if (item.Sources.Contains(5) && FilterValue.HasFlag(ItemFilter.MonsterDrop)) return true;
                if (item.Sources.Contains(-1) && FilterValue.HasFlag(ItemFilter.Unknown)) return true;


                return false;
            }
        }

        private void OpenContextMenu(object? sender, Ingredient item)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(item.Data.Name.RawString);

            using var popup = ImRaii.Popup(item.Data.Name.RawString);
            if (!popup)
                return;

            DrawGatherItem(item);
            DrawSearchItem(item);
            DrawItemVendorLookup(item);
            DrawMonsterLootLookup(item);
            DrawMarketBoardLookup(item);
            DrawFilterOnCrafts(item);
            DrawRestockFromRetainer(item);
            //DrawCraftThisItem(item);
        }

        private void DrawMarketBoardLookup(Ingredient item)
        {

            if (item.Data.RowId == 0)
                return;

            if (Marketboard)
            {
                if (ImGui.Selectable("Market Board Lookup"))
                {
                    Chat.Instance.SendMessage($"/pmb {item.Data.Name.ToDalamudString()}");
                }
            }
        }

        private void DrawCraftThisItem(Ingredient item)
        {
            if (item.Data.RowId == 0 || !item.CanBeCrafted)
                return;

            if (item.TotalCraftable == 0)
            {
                using var disabled = ImRaii.Disabled();
            }


            if (ImGui.GetIO().KeyShift)
            {
                if (ImGui.Selectable("Craft this Item (Buffless)"))
                {
                    Endurance.RecipeID = item.CraftedRecipe.RowId;
                }

                ImGuiComponents.HelpMarker("Starts crafting this item skipping applying food/potion buffs.");
                return;
            }

            if (ImGui.GetIO().KeyCtrl)
            {
                bool disabled = !item.CraftedRecipe.CanQuickSynth;

                if (disabled)
                    ImGui.BeginDisabled();

                if (ImGui.Selectable("Craft this Item (Quick Synth)"))
                {
                    P.TM.Enqueue(() => CraftingListFunctions.OpenRecipeByID(item.CraftedRecipe.RowId));
                    P.TM.Enqueue(() => CraftingListFunctions.SwitchJobGearset(item.CraftedRecipe.CraftType.Row + 8));
                    P.TM.Enqueue(() => CurrentCraftMethods.QuickSynthItem(item.Required));
                }

                ImGuiComponents.HelpMarker("Quick Synths up to the required amount.");

                if (disabled)
                    ImGui.EndDisabled();

                return;
            }


            if (ImGui.Selectable("Craft this Item"))
            {
                Endurance.RecipeID = item.CraftedRecipe.RowId;
            }

            ImGuiComponents.HelpMarker("Hold Shift to craft without applying food/potion buffs.\r\nHold Ctrl to quick synth this item.");


        }

        private void DrawRestockFromRetainer(Ingredient item)
        {
            if (item.Data.RowId == 0 || item.RetainerCount == 0 || item.Required <= item.Inventory)
                return;

            if (RetainerInfo.GetReachableRetainerBell() == null)
            {
                ImGui.TextDisabled($"Fetch From Retainer (please stand by a bell)");
            }
            else
            {
                if (RetainerInfo.TM.IsBusy)
                {
                    ImGui.TextDisabled($"Currently fetching. Please wait.");
                    return;
                }

                if (!ImGui.Selectable("Fetch From Retainer"))
                    return;

                var howManyToGet = item.Required - item.Inventory;
                if (howManyToGet > 0)
                {
                    RetainerInfo.RestockFromRetainers(item.Data.RowId, howManyToGet);
                }
            }
        }

        private void DrawFilterOnCrafts(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (FilteredItems.Count() == Items.Count() || Headers.Any(x => x.FilterFunc(item)))
            {
                if (item.Sources.Contains(1) && item.OriginList.Items.Any(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.Row == item.Data.RowId && y.RowId == x)))
                {
                    if (ImGui.Selectable($"Show ingredients used for this"))
                    {
                        FilteredItems.Clear();
                        var idx = 0;
                        FilteredItems.Add((item, idx));
                        idx++;
                        foreach (var ingredient in CraftingListHelpers.GetIngredientRecipe(item.Data.RowId).UnkData5.Where(x => x.AmountIngredient > 0))
                        {
                            if (Items.FindFirst(x => x.Data.RowId == ingredient.ItemIngredient, out var result))
                                FilteredItems.Add((result, idx));
                            idx++;
                        }

                        CraftFiltered = true;
                    }
                }
            }

            if (CraftFiltered)
            {
                if (!ImGui.Selectable($"Clear Filters"))
                    return;

                CraftFiltered = false;
                FilterDirty = true;

            }
        }

        private static void DrawMonsterLootLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (MonsterLookup)
            {
                if (!ImGui.Selectable("Monster Loot Lookup"))
                    return;

                try
                {
                    Chat.Instance.SendMessage($"/mloot {item.Data.Name.RawString}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("Monster Loot Lookup (Please install Monster Loot Hunter)");
            }
        }

        private static void DrawItemVendorLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (ItemVendor)
            {
                if (IPC.ItemVendorLocation.ItemHasVendor(item.Data.RowId))
                {
                    if (!ImGui.Selectable("Item Vendor Lookup"))
                        return;

                    try
                    {
                        IPC.ItemVendorLocation.OpenContextMenu(item.Data.RowId);
                    }
                    catch (Exception e)
                    {
                        e.Log();
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Item Vendor Lookup (Please install Item Vendor Location)");
            }
        }

        private static void DrawSearchItem(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (!ImGui.Selectable("Search for Item"))
                return;

            try
            {
                SearchItem(item.Data.RowId);
            }
            catch (Exception e)
            {
                e.Log();
            }

        }

        private static void DrawGatherItem(Ingredient item)
        {
            if (item.Data.RowId == 0 || item.Sources.Contains(1))
                return;

            if (GatherBuddy)
            {
                if (!ImGui.Selectable("Gather Item"))
                    return;

                try
                {
                    if (LuminaSheets.GatheringItemSheet!.Any(x => x.Value.Item == item.Data.RowId))
                        Chat.Instance.SendMessage($"/gather {item.Data.Name.RawString}");
                    else
                        Chat.Instance.SendMessage($"/gatherfish {item.Data.Name.RawString}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("Gather Item (Please install Gatherbuddy)");
            }
        }
    }

    [Flags]
    public enum ItemFilter
    {
        NoItems = 0,
        MissingItems = 1,
        NoMissingItems = 2,

        Crafted = 4,
        Gathered = 8,
        Fishing = 16,
        Vendor = 32,
        MonsterDrop = 64,
        Unknown = 128,

        NonCrystals = 256,
        Crystals = 512,

        GatherZone = 4096,
        NoGatherZone = 8192,
        TimedNode = 16384,
        NonTimedNode = 32768,

        All = MissingItems + NoMissingItems +
                Crafted + Gathered + Fishing + Vendor + MonsterDrop + Unknown +
                NonCrystals + Crystals +
                GatherZone + NoGatherZone + TimedNode + NonTimedNode,
    }
}
