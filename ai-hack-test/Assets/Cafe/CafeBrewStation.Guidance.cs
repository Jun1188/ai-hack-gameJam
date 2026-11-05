using System;
using CafeSim;
using UnityEngine;

namespace MapleCafe
{
    public sealed partial class CafeBrewStation
    {
        // Read the same ingredient labels and state used by Interact; guidance never advances the brew.
        internal bool TryGetRecipeGuidance(MenuItem recipe, bool iced, out Collider target, out string instruction)
        {
            target = null;
            instruction = null;
            if (recipe == null) return false;
            if (busy) { instruction = status.text; return true; }
            if (finishedDrink != null)
            {
                target = holdingMug ? Receiver?.Target : mugTarget;
                instruction = holdingMug
                    ? finishedDrink == InvalidDrinkName ? "손님에게 건네기 · 신비한 음료는 별점 1점" : "손님에게 완성된 음료 건네기"
                    : "완성된 머그잔 들기";
                return true;
            }
            if (holdingBlend) { target = mugTarget; instruction = "블렌딩한 음료를 머그잔에 따르기"; return true; }

            var labels = recipe.recipe.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
            int required = 0;
            foreach (string label in labels)
            {
                var ingredient = Array.Find(ingredients, i => i.label == label);
                if (ingredient == null) return false;
                required |= 1 << (int)ingredient.kind;
            }
            // One water portion can precede the ice and becomes the ICE base, rather than a recipe ingredient.
            int contents = iced && !hasIce && portions == 1 && blendIngredients == WaterBit ? 0 : blendIngredients;
            if ((contents & ~required) != 0 || (!iced && hasIce))
            {
                target = sinkTarget; instruction = "다른 재료가 들어갔어요 · 싱크대에서 비우고 다시 시작"; return true;
            }
            Ingredient next = null;
            string phase = "";
            if (iced && !hasIce) { next = Array.Find(ingredients, i => i.kind == IngredientKind.Ice); phase = "ICE 준비 · "; }
            else if (iced && !iceWaterReady) { next = Array.Find(ingredients, i => i.kind == IngredientKind.Water); phase = "ICE 준비 · "; }
            else
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    var candidate = Array.Find(ingredients, v => v.label == labels[i]);
                    if ((blendIngredients & (1 << (int)candidate.kind)) != 0) continue;
                    next = candidate; phase = $"재료 {i + 1}/{labels.Length} · "; break;
                }
            }
            if (heldIngredient != null)
            {
                if (next == heldIngredient)
                { target = mixingTarget; instruction = phase + heldIngredient.label + " 제조 컵에 넣기"; }
                else { target = heldIngredient.target; instruction = heldIngredient.label + " 제자리에 내려놓기"; }
                return true;
            }
            if (next == null)
            {
                if (shotPrep != ShotPrep.None) { target = sinkTarget; instruction = "남은 원두를 싱크대에서 비우기"; }
                else { target = mixingTarget; instruction = "재료 준비 완료 · 제조 컵에서 블렌딩하기"; }
                return true;
            }
            if (next.kind == IngredientKind.Shot)
            {
                target = shotPrep == ShotPrep.None ? beanTarget : shotPrep == ShotPrep.Beans ? grinderTarget : espressoTarget;
                instruction = phase + (shotPrep == ShotPrep.None ? "원두 집기" : shotPrep == ShotPrep.Beans ? "그라인더에서 원두 갈기" : "머신에서 에스프레소 샷 추출하기");
            }
            else { target = next.target; instruction = phase + next.label + " 집기"; }
            return true;
        }
    }
}
