using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using System.Xml;
using System.Globalization;
using System.Collections;
using System.IO;
using UnityEngine;
using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using AssetBundles;
using UnityEngine.UI;

namespace RewardPatchPerCharacter
{
    [HarmonyPatch(typeof(S))]
    [HarmonyPatch("Awake")]
    static class CharactersIgnoringRewards
    {
        public static List<string> ignoring_spells = new List<string>();
        public static List<string> ignoring_artifacts = new List<string>();
        public static List<string> shop_disabled = new List<string>();
        static void Prepare()
        {
            ignoring_spells = new List<string>();
            ignoring_artifacts = new List<string>();
            shop_disabled = new List<string>();
        }

        public static void Switch(BeingObject beingObj, XmlReader reader)
        {
            switch (reader.Name)
            {
                case "SkipSpellRewards":
                    ignoring_spells.Add(beingObj.beingID);
                    Debug.Log(beingObj.beingID + " is ignoring Spell Rewards!");
                    break;
                case "SkipArtifactRewards":
                    ignoring_artifacts.Add(beingObj.beingID);
                    Debug.Log(beingObj.beingID + " is ignoring Artifact Rewards!");
                    break;
                case "DisableShop":
                    shop_disabled.Add(beingObj.beingID);
                    Debug.Log(beingObj.beingID + " has disabled the shop!");
                    break;
                default:
                    break;
            }
        }
    }

    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(PostCtrl), nameof(PostCtrl.GenerateLootOptions))]
    static class PostCtrlPatches_GenerateLootPerCharacter
    {
        static void Prefix(PostCtrl __instance, RewardType rewardType)
        {
            if (CharactersIgnoringRewards.ignoring_artifacts.Contains(S.I.batCtrl.currentHeroObj.beingID))
            {
                __instance.remainingArtDrops.Clear();
            }
        }

        //Should remove loot
        static void Postfix(PostCtrl __instance, RewardType rewardType)
        {
            if (CharactersIgnoringRewards.ignoring_spells.Contains(S.I.batCtrl.currentHeroObj.beingID))
            {
                if (!(rewardType == RewardType.ArtDrop || rewardType == RewardType.BossArt || rewardType == RewardType.LevelUp))
                {
                    var coroutine = Util_SpellLootDestroyer.WaitAndEndLoot(0.9f, __instance, rewardType);
                    __instance.StartCoroutine(coroutine);
                }
            }
            if (CharactersIgnoringRewards.ignoring_artifacts.Contains(S.I.batCtrl.currentHeroObj.beingID))
            {
                if (rewardType == RewardType.BossArt)
                {
                    var coroutine = Util_ArtifactLootDestroyer.WaitAndEndLoot(0.9f, __instance, rewardType);
                    __instance.StartCoroutine(coroutine);
                }
            }
            
        }


    }
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(PostCtrl), nameof(PostCtrl.StartLevelUpOptions))]
    static class PostCtrlPatches_LevelUpPerCharacter
    {

        //Should remove levelup
        static void Postfix(PostCtrl __instance)
        {
            if (CharactersIgnoringRewards.ignoring_artifacts.Contains(S.I.batCtrl.currentHeroObj.beingID))
            {
                var coroutine = Util_ArtifactLootDestroyer.WaitAndEndLevelUp(0.6f, __instance);
                __instance.StartCoroutine(coroutine);
            }
        }

    }

    static class Util_SpellLootDestroyer
    {
        public static IEnumerator WaitAndEndLoot(float waitTime, PostCtrl postCtrl, RewardType rt)
        {
            yield return new WaitForSeconds(waitTime);
            postCtrl.EndLoot(rt, true);
        }
    }

    static class Util_ArtifactLootDestroyer
    {
        public static IEnumerator WaitAndEndLoot(float waitTime, PostCtrl postCtrl, RewardType rt)
        {
            yield return new WaitForSeconds(waitTime);
            postCtrl.EndLoot(rt, true);
        }

        public static IEnumerator WaitAndEndLevelUp(float waitTime, PostCtrl postCtrl)
        {
            yield return new WaitForSeconds(waitTime);
            var method = AccessTools.Method(typeof(PostCtrl), "ClearAndHideCards");
            method.Invoke(postCtrl, null);
        }
    }

    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(ShopCtrl), nameof(PostCtrl.Open))]
    static class ShopCtrlPatches_OpenPerCharacter
    {

        static bool Prefix(ShopCtrl __instance)
        {
            return !CharactersIgnoringRewards.shop_disabled.Contains(S.I.batCtrl.currentHeroObj.beingID);
        }

    }

    [HarmonyPatch(typeof(BeingObject))]
    [HarmonyPatch("ReadXmlPrototype")]
    static class CustomBoss_ReadXmlPrototype
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            var index = -1;

            var to_call = AccessTools.Method(typeof(CharactersIgnoringRewards), nameof(CharactersIgnoringRewards.Switch));

            for (int i = 0; i < code.Count(); i++)
            {
                if (index == -1 && code[i].opcode == OpCodes.Brtrue)
                {
                    index = i + 1;
                } else if (code[i].opcode == OpCodes.Call && (MethodInfo)(code[i].operand) == to_call)
                {
                    Debug.LogError("ReadXmlPrototype Transpiler already went!");
                    return code.AsEnumerable();
                }
            }
            if (index == -1)
            {
                Debug.LogError("ReadXmlPrototype Transpiler failed!");
                return code.AsEnumerable();
            }

            var to_add = new List<CodeInstruction>();

            to_add.Add(new CodeInstruction(OpCodes.Ldarg_0));
            to_add.Add(new CodeInstruction(OpCodes.Ldloc_0));
            to_add.Add(new CodeInstruction(OpCodes.Call, to_call));

            code.InsertRange(index, to_add.AsEnumerable());

            return code.AsEnumerable();
        }

    }
}