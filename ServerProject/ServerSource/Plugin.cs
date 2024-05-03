using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Barotrauma;

namespace TeleportDuffelBags
{
    public partial class Plugin : IAssemblyPlugin
    {
        private static readonly MethodBase rGiveJobItems = LuaCsHook.ResolveMethod("Barotrauma.Character", "GiveJobItems", new string[] { "Barotrauma.WayPoint" });
        private static readonly MethodBase rEnd = LuaCsHook.ResolveMethod("Barotrauma.CampaignMode", "End", null);
        private static readonly MethodBase rItemRemove = LuaCsHook.ResolveMethod("Barotrauma.Item", "Remove", Array.Empty<string>());
        
        private void InitServer()
        {
            GameMain.LuaCs.Hook.Patch(
                "TDB_GiveJobItemsDuffelBag",
                rGiveJobItems,
                (instance, ptable) =>
                {
                    if (instance is not Character character) return null;
                    if (character?.Info.StartItemsGiven ?? true) return null;
                    var containers = new HashSet<Item>();
                    foreach (var item in Item.ItemList)
                    {
                        if ((item.Container?.HasTag(Tags.DespawnContainer) ?? false) && (item.Container?.HasTag($"name:{character.Name}") ?? false) )
                        {
                            Item container = item.Container;
                            var wearSlot = item.allowedSlots
                                .Where(type => (type & ~(InvSlotType.Any | InvSlotType.LeftHand | InvSlotType.RightHand)) != 0) // remove slots that are not wear slots
                                .ToHashSet();
                            
                            if (wearSlot != item.allowedSlots && wearSlot.Any())
                            {
                                if (character.Inventory.TryPutItem(item, null, wearSlot))
                                {
                                    containers.Add(container);
                                    continue;
                                }
                            }
                            if (character.Inventory.TryPutItem(item, null, item.allowedSlots))
                            {
                                containers.Add(container);
                            }
                            else
                            {
                                ModUtils.Logging.PrintWarning($"[TDB] Failed to put item {item.Name} into character {character.Name} inventory");
                            }
                        }
                    }
                    if (containers.Any())
                    {
                        ptable.PreventExecution = true;
                        foreach (Item item in containers)
                        {
                            if (item.Removed) continue;
                            if (!(item.ContainedItems.Any()))
                            {
                                Entity.Spawner?.AddItemToRemoveQueue(item);
                            }
                        }
                    }
                    return null;
                },
                LuaCsHook.HookMethodType.Before);
            
            
            GameMain.LuaCs.Hook.Patch(
                "TDB_EndBefore",
                rEnd,
                (_, _) =>
                {
                    GameMain.LuaCs.Hook.Patch(
                        "TDB_ItemRemove",
                        rItemRemove,
                        (instance, ptable) =>
                        {
                            if (instance is not Item item) return null;
                            if (item.HasTag(Tags.IdCardTag) && (item.Container?.HasTag(Tags.DespawnContainer) ?? false))
                                ptable.PreventExecution = true;
                            return null;
                        });
                    return null;
                },
                LuaCsHook.HookMethodType.Before);
            
            
            GameMain.LuaCs.Hook.Patch(
                "TDB_EndAfter",
                rEnd,
                (_, _) =>
                {
                    GameMain.LuaCs.Hook.RemovePatch(
                        "TDB_ItemRemove",
                        rItemRemove,
                        LuaCsHook.HookMethodType.Before);
                    return null;
                },
                LuaCsHook.HookMethodType.After);
        }
        
        private void DisposeServer()
        {
            GameMain.LuaCs.Hook.RemovePatch(
                "TDB_GiveJobItemsDuffelBag",
                rGiveJobItems,
                LuaCsHook.HookMethodType.Before);
            GameMain.LuaCs.Hook.RemovePatch(
                "TDB_EndBefore",
                rEnd,
                LuaCsHook.HookMethodType.Before);
            GameMain.LuaCs.Hook.RemovePatch(
                "TDB_EndAfter",
                rEnd,
                LuaCsHook.HookMethodType.After);
        }
    }
}
