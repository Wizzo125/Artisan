﻿using Artisan.CraftingLists;
using Artisan.IPC;
using Dalamud.Logging;
using ECommons.DalamudServices;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    public class Ingredient
    {
        public Item Data;
        public TextureWrap Icon;
        public int Required;
        public int Inventory => CraftingListUI.NumberOfIngredient(Data.RowId);
        public int RetainerCount => RetainerInfo.GetRetainerItemCount(Data.RowId);
        public int Remaining
        {
            get
            {
                var current = Math.Max(0, Required - Inventory - RetainerCount - (CanBeCrafted ? TotalCraftable : 0) - AmountUsedForSubcrafts);
                if (remaining != current)
                {
                    remaining = current;
                    OnRemainingChange?.Invoke(this, true);
                }
                return remaining;
            }
        }
        private int remaining;

        public bool CanBeCrafted;
        public string CraftingJobs;
        public int TotalCraftable => NumberCraftable(Data.RowId);
        public List<uint> UsedInCrafts = new();
        public int MaterialIndex;
        public uint Category;
        public CraftingList OriginList;
        private Dictionary<uint, int> UsedInMaterialsList;
        public int AmountUsedForSubcrafts => GetSubCraftCount();
        public TerritoryType GatherZone;
        public bool TimedNode;
        private Dictionary<int, Recipe?> subRecipes = new();

        public virtual event EventHandler<bool> OnRemainingChange;

        private int GetSubCraftCount()
        {
            int output = 0;
            foreach (var material in UsedInMaterialsList)
            {
                var owned = RetainerInfo.GetRetainerItemCount(material.Key) + CraftingListUI.NumberOfIngredient(material.Key);
                var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == material.Key && x.UnkData5.Any(y => y.ItemIngredient == Data.RowId));
                var numberUsedInRecipe = recipe.UnkData5.First(x => x.ItemIngredient == Data.RowId).AmountIngredient;

                output += Math.Min(owned * numberUsedInRecipe, material.Value * numberUsedInRecipe);
            }
            return output;
        }

        public Ingredient(uint itemId, int required, CraftingList originList, Dictionary<uint, int> materials)
        {
            Data = LuminaSheets.ItemSheet.Values.First(x => x.RowId == itemId);
            Icon = P.Icons.LoadIcon(Data.Icon);
            Required = required;
            CanBeCrafted = LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == itemId);
            GatherZone = Svc.Data.Excel.GetSheet<TerritoryType>()!.First(x => x.RowId == 1);
            TimedNode = false;
            if (LuminaSheets.GatheringItemSheet!.FindFirst(x => x.Value.Item == Data.RowId, out var gather))
            {
                if (HiddenItems.Items.FindFirst(x => x.ItemId == Data.RowId, out var hiddenItems))
                {
                    if (Svc.Data.Excel.GetSheet<GatheringPoint>()!.FindFirst(y => y.GatheringPointBase.Row == hiddenItems.NodeId && y.TerritoryType.Value.PlaceName.Row > 0, out var gatherpoint))
                    {
                        var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                        if (transient.GatheringRarePopTimeTable.Row > 0)
                            TimedNode = true;

                        if (gatherpoint.TerritoryType.IsValueCreated)
                            GatherZone = gatherpoint.TerritoryType.Value!;
                    }
                }
                else
                {
                    foreach (var pointBase in LuminaSheets.GatheringPointBaseSheet.Values.Where(x => x.Item.Any(y => y == gather.Key)))
                    {
                        if (GatherZone.RowId != 1) break;
                        if (Svc.Data.Excel.GetSheet<GatheringPoint>()!.FindFirst(y => y.GatheringPointBase.Row == pointBase.RowId && y.TerritoryType.Value.PlaceName.Row > 0, out var gatherpoint))
                        {
                            var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                            if (transient.GatheringRarePopTimeTable.Row > 0)
                                TimedNode = true;

                            if (gatherpoint.TerritoryType.IsValueCreated)
                                GatherZone = gatherpoint.TerritoryType.Value!;
                        }
                    }
                }
            }

            Category = Data.ItemSearchCategory.Row;
            OriginList = originList;
            foreach (var recipe in OriginList.Items)
            {
                if (LuminaSheets.RecipeSheet[recipe].UnkData5.Any(x => x.ItemIngredient == itemId) && !UsedInCrafts.Contains(recipe))
                    UsedInCrafts.Add(recipe);
            }
            UsedInMaterialsList = materials.Where(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.Row == x.Key && y.UnkData5.Any(z => z.ItemIngredient == Data.RowId))).ToDictionary(x => x.Key, x => x.Value);
        }

        private string JobFromCraftType(uint row)
        {
            return LuminaSheets.ClassJobSheet[row + 8].Abbreviation;
        }

        private int NumberCraftable(uint itemId)
        {
            List<int> NumberOfUses = new();
            if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId, out var recipe))
            {
                foreach (var ingredient in recipe.UnkData5.Where(x => x.AmountIngredient > 0))
                {
                    int invCount = CraftingListUI.NumberOfIngredient((uint)ingredient.ItemIngredient);
                    int retainerCount = RetainerInfo.GetRetainerItemCount((uint)ingredient.ItemIngredient);

                    int craftableCount = 0;
                    Recipe? subRecipe;
                    if (subRecipes.ContainsKey(ingredient.ItemIngredient))
                    {
                        subRecipe = subRecipes[ingredient.ItemIngredient];
                    }
                    else
                    {
                        subRecipe = CraftingListHelpers.GetIngredientRecipe((uint)ingredient.ItemIngredient);
                        subRecipes.Add(ingredient.ItemIngredient, subRecipe);
                    }

                    if (subRecipe is not null)
                    {
                        craftableCount = NumberCraftable((uint)ingredient.ItemIngredient);
                    }

                    int total = invCount + retainerCount + craftableCount;

                    int uses = total / ingredient.AmountIngredient;

                    NumberOfUses.Add(uses);
                }

                return NumberOfUses.Min() * recipe.AmountResult;
            }
            else
                return -1;
        }

        public async static Task<List<Ingredient>> GenerateList(CraftingList originList)
        {
            var materials = originList.ListMaterials();
            List<Ingredient> output = new();
            foreach (var item in materials)
            {
               await Task.Run(() => output.Add(new Ingredient(item.Key, item.Value, originList, materials)));
            }

            return output;
        }


    }

}
