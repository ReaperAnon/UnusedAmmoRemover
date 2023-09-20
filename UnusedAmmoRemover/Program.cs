using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Noggog;

namespace UnusedAmmoRemover
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.Fallout4, "YourPatcher.esp")
                .Run(args);
        }

        static Dictionary<IFormLinkGetter<IAmmunitionGetter>, bool> AmmoDict { get; } = new();

        public static void RunPatch(IPatcherState<IFallout4Mod, IFallout4ModGetter> state)
        {
            foreach (var weaponGetter in state.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>())
                AmmoDict[weaponGetter.Ammo] = true;

            foreach (var objModGetter in state.LoadOrder.PriorityOrder.WinningOverrides<IAObjectModificationGetter>())
            {
                if (objModGetter is not IWeaponModificationGetter weaponModGetter)
                    continue;

                foreach (var entry in weaponModGetter.Properties)
                {
                    if (entry is IObjectModFormLinkIntPropertyGetter<Weapon.Property> propGetter && propGetter.Property == Weapon.Property.Ammo && propGetter.Record.TryResolve(state.LinkCache, out var recordGetter))
                    {
                        if (recordGetter is not IAmmunitionGetter ammoGetter)
                            continue;

                        AmmoDict[ammoGetter.ToLinkGetter()] = true;
                    }
                }
            }

            // Remove ammo.
            foreach (var lvliGetter in state.LoadOrder.PriorityOrder.WinningOverrides<ILeveledItemGetter>())
            {
                bool wasChanged = false;
                var lvliSetter = lvliGetter.DeepCopy();
                for (int i = (lvliGetter.Entries?.Count ?? 0) - 1; i >= 0; i--)
                {
                    if (lvliGetter.Entries![i].Data is null || !lvliGetter.Entries[i].Data!.Reference.TryResolve(state.LinkCache, out var itemGetter))
                        continue;

                    if (itemGetter is IAmmunitionGetter ammoGetter && !AmmoDict.TryGetValue(ammoGetter.ToLinkGetter(), out _))
                    {
                        wasChanged |= true;
                        lvliSetter.Entries!.RemoveAt(i);
                    }
                }

                if (wasChanged)
                    state.PatchMod.LeveledItems.Set(lvliSetter);
            }

            foreach (var constrObjGetter in state.LoadOrder.PriorityOrder.WinningOverrides<IConstructibleObjectGetter>())
            {
                if (!constrObjGetter.CreatedObject.TryResolve(state.LinkCache, out var constrObj))
                    continue;

                if (constrObj is not IAmmunitionGetter ammoGetter)
                    continue;

                if (!AmmoDict.TryGetValue(ammoGetter.ToLinkGetter(), out _))
                {
                    var constrObjSetter = state.PatchMod.ConstructibleObjects.GetOrAddAsOverride(constrObjGetter);
                    constrObjSetter.CreatedObject.SetToNull();
                    constrObjSetter.WorkbenchKeyword.SetToNull();
                }
            }
        }
    }
}
