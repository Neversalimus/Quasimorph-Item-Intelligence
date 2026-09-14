using System;
using System.Collections.Generic;
using System.Reflection;
using MGSC;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Only immutable API metadata is retained. Station stock, price inputs,
        // faction reputation, difficulty and progression are supplied on every call.
        // TypeByName scans assemblies in the shipped Harmony; keep it out of pricing.
        private static class TradePriceApi103
        {
            public static readonly MethodInfo BuyPrice = Resolve("GetBuyPrice",
                typeof(MagnumProgression), typeof(Factions), typeof(ItemsPrices),
                typeof(Station), typeof(Dictionary<string, int>));
            public static readonly MethodInfo SellPrice = Resolve("GetItemSellPrice",
                typeof(MagnumProgression), typeof(Faction), typeof(Station),
                typeof(ItemsPrices), typeof(string), typeof(bool));
            public static readonly MethodInfo SellTradePoints = Resolve("GetItemSellTradePoints",
                typeof(MagnumProgression), typeof(Faction), typeof(Station),
                typeof(ItemsPrices), typeof(Difficulty), typeof(BasePickupItem),
                typeof(Dictionary<string, int>), typeof(bool));

            private static MethodInfo Resolve(string name, params Type[] signature)
            {
                // Resolving a similarly named overload must never enable exact prices.
                MethodInfo method = typeof(TradeSystem).GetMethod(name, StaticFlags, null, signature, null);
                if (method == null || method.ReturnType != typeof(int)) return null;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != signature.Length) return null;
                for (int i = 0; i < parameters.Length; i++)
                    if (parameters[i].ParameterType != signature[i]) return null;
                return method;
            }
        }

        private static class TradeStockApi
        {
            public static readonly MethodInfo CountItems = typeof(ItemStorage).GetMethod(
                "CountItems", InstanceFlags, null, new Type[] { typeof(string) }, null);
        }
    }
}
