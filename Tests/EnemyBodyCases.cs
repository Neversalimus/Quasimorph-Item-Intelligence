namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int bodyChecks;
        private static void BodyAssert(bool condition, string label)
        { bodyChecks++; if (!condition) throw new Exception("Enemy body regression: " + label); }
        private static void BodyNear(double actual, double expected, string label)
        { BodyAssert(!double.IsNaN(actual) && Math.Abs(actual - expected) < 1e-10, label + " actual=" + actual + " expected=" + expected); }
        private static EnemyBodySlot TestSlot(string type, string nature, int min, int max)
        { return new EnemyBodySlot { Id = type + "_" + nature, Type = type, Nature = nature, MinSockets = min, MaxSockets = max }; }
        private static EnemyImplantRule TestImplant(string id, string type, string nature)
        { return new EnemyImplantRule { Id = id, Type = type, Natures = new HashSet<string>(new[] { nature }, StringComparer.Ordinal) }; }
        private static EnemyBodyModel TestBody(params EnemyBodySlot[] slots)
        {
            EnemyBodyModel body = new EnemyBodyModel();
            foreach (EnemyBodySlot slot in slots) body.Slots[slot.Type] = new List<EnemyBodySlot> { slot };
            return body;
        }
        private static Dictionary<string, double> TestProject(EnemyBodyModel body, EnemyImplantRule[] rules,
            double[] weights, int min, int max, List<EnemyImplantRule> grants = null, double gate = 1.0)
        {
            Dictionary<string, EnemyImplantRule> records = new Dictionary<string, EnemyImplantRule>();
            Dictionary<string, double> pool = new Dictionary<string, double>();
            for (int i = 0; i < rules.Length; i++) { records[rules[i].Id] = rules[i]; pool[rules[i].Id] = weights[i]; }
            return ProjectRandomEnemyImplants(body, records, pool, grants ?? new List<EnemyImplantRule>(), gate, min, max);
        }
        // Independent exhaustive oracle: generate entire sequences, then install
        // them in order in a bounded socket array. Category 2 consumes a draw only.
        private static double EnumerateInstallSequences(int[] sequence, int index, int sockets,
            double[] probabilities)
        {
            if (index == sequence.Length)
            {
                List<int> installed = new List<int>();
                foreach (int item in sequence)
                    if (item != 2 && installed.Count < sockets) installed.Add(item);
                return installed.Contains(0) ? 1.0 : 0.0;
            }
            double sum = 0.0;
            for (int item = 0; item < 3; item++)
            {
                sequence[index] = item;
                sum += probabilities[item] * EnumerateInstallSequences(sequence, index + 1, sockets, probabilities);
            }
            return sum;
        }
        public static int RunEnemyBodyCases()
        {
            string previousSha = _compatAssemblySha256;
            string sha582 = "9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006";
            foreach (string fingerprint in new[] { previousSha, previousSha.ToLowerInvariant(), sha582,
                sha582.ToLowerInvariant(), sha582.Substring(0, 63) + "7", "unknown", "", null })
            {
                _compatAssemblySha256 = fingerprint;
                bool known = string.Equals(fingerprint, previousSha, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fingerprint, sha582, StringComparison.OrdinalIgnoreCase);
                BodyAssert(IsAuditedEnemyBodyAssembly() == known, "body assembly scope excludes unknown builds");
                Data.BodyTypes.Values.Clear();
                Data.BodyTypes.Values.Add(new BodyTypeRecord { Id = "gate_body", WoundSlots = new List<string>() });
                var gateModels = ReadEnemyBodyModels(new MobClassRecord { BodyTypes = new List<string> { "gate_body" } });
                BodyAssert(gateModels.Count == 1 && gateModels[0].Unknown == !known, "body model follows independent audit");
            }
            _compatAssemblySha256 = previousSha;
            EnemyImplantRule a = TestImplant("a", "head", "flesh");
            EnemyImplantRule b = TestImplant("b", "head", "flesh");
            EnemyImplantRule missing = TestImplant("tail", "tail", "flesh");
            EnemyImplantRule incompatible = TestImplant("metal", "head", "metal");
            EnemyBodyModel body = TestBody(TestSlot("head", "flesh", 1, 1));
            var rows = TestProject(body, new[] { a, missing }, new[] { 1.0, 1.0 }, 1, 1);
            BodyNear(rows["a"], 1.0, "missing slot removed BEFORE normalization");
            BodyAssert(!rows.ContainsKey("tail"), "phantom slot source absent");
            rows = TestProject(body, new[] { a, incompatible }, new[] { 1.0, 1.0 }, 1, 1);
            BodyNear(rows["a"], 0.5, "incompatible nature remains in denominator");
            BodyAssert(!rows.ContainsKey("metal"), "never-installable nature source absent");
            rows = TestProject(body, new[] { a, incompatible }, new[] { 1.0, 1.0 }, 2, 2);
            BodyNear(rows["a"], 0.75, "failed nature attempt does not occupy a socket");
            rows = TestProject(body, new[] { a, b }, new[] { 1.0, 1.0 }, 3, 3);
            BodyNear(rows["a"], 0.5, "one socket remains 50%, not 87.5%, after three draws");
            rows = TestProject(TestBody(TestSlot("head", "flesh", 2, 2)), new[] { a, b }, new[] { 1.0, 1.0 }, 3, 3);
            BodyNear(rows["a"], 0.75, "two sockets cap successful selections");
            rows = TestProject(body, new[] { a, b }, new[] { 1.0, 1.0 }, 2, 2, null, 0.5);
            BodyNear(rows["a"], 0.375, "None gate consumes attempts without occupying slots");
            rows = TestProject(body, new[] { a }, new[] { 1.0 }, 2, 2, new List<EnemyImplantRule> { b });
            BodyAssert(rows.Count == 0, "granted implant occupies the only socket");
            rows = TestProject(body, new[] { a }, new[] { 1.0 }, 1, 1, new List<EnemyImplantRule> { incompatible });
            BodyNear(rows["a"], 1.0, "failed grant leaves the socket free");
            body = TestBody(TestSlot("head", "flesh", 0, 2));
            rows = TestProject(body, new[] { a, b }, new[] { 1.0, 1.0 }, 2, 2);
            BodyNear(rows["a"], (0.0 + 0.5 + 0.75) / 3, "uniform augmentation socket count integrated");
            BodyNear(ProjectGrantedEnemyImplant(body, a, new List<EnemyImplantRule> { a, a }), 2.0 / 3,
                "duplicate grants do not make a zero-socket body installable");
            BodyNear(ProjectGrantedEnemyImplant(body, a, new List<EnemyImplantRule> { b, a }), 1.0 / 3,
                "grant order competes for random capacity");
            BodyNear(ProjectGrantedEnemyImplant(body, a, new List<EnemyImplantRule> { incompatible, a }), 2.0 / 3,
                "incompatible earlier grant does not consume capacity");
            body = TestBody(TestSlot("head", "flesh", 1, 1));
            var metalHead = new EnemyAugmentationRule { Id = "metal_head", Slots = new List<EnemyBodySlot> { TestSlot("head", "metal", 1, 1) } };
            ApplyGuaranteedEnemyAugmentation(body, metalHead);
            rows = TestProject(body, new[] { a, incompatible }, new[] { 1.0, 1.0 }, 1, 1);
            BodyAssert(!rows.ContainsKey("a"), "guaranteed augmentation replaces old nature");
            BodyNear(rows["metal"], 0.5, "old incompatible nature still consumes selections");
            var conflict = new EnemyAugmentationRule { Id = "combined", Slots = new List<EnemyBodySlot> {
                TestSlot("head", "flesh", 1, 1), TestSlot("tail", "flesh", 1, 1) } };
            ApplyGuaranteedEnemyAugmentation(body, conflict);
            BodyAssert(!body.Slots.ContainsKey("tail") && !body.GrantedAugs.Contains("combined"), "combined grant conflicts atomically");
            IncludePossibleEnemyAugmentation(body, conflict);
            BodyAssert(!body.Conditional && !body.Slots.ContainsKey("tail"), "random augmentation cannot override a granted conflict");
            body = TestBody(TestSlot("head", "flesh", 1, 1));
            IncludePossibleEnemyAugmentation(body, new EnemyAugmentationRule { Id = "new_tail", Slots = new List<EnemyBodySlot> { TestSlot("tail", "flesh", 1, 1) } });
            rows = TestProject(body, new[] { a, missing, incompatible }, new[] { 1.0, 1.0, 1.0 }, 1, 1);
            BodyAssert(double.IsNaN(rows["tail"]) && double.IsNaN(rows["a"]), "randomly added slot preserves possible sources but hides numeric odds");
            BodyAssert(!rows.ContainsKey("metal"), "incompatible in all possible bodies is still excluded");
            EnemyBodyModel copy = body.Copy();
            copy.Slots["head"].Clear();
            BodyAssert(body.Slots["head"].Count == 1, "context snapshots do not mutate each other");
            body = new EnemyBodyModel { Unknown = true };
            rows = TestProject(body, new[] { a }, new[] { 1.0 }, 1, 1);
            BodyAssert(double.IsNaN(rows["a"]), "missing body data does not invent a zero or a percentage");
            body = TestBody(TestSlot("head", "flesh", 1, 1));
            foreach (double invalid in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            {
                rows = TestProject(body, new[] { a, b }, new[] { 1.0, invalid }, 1, 1);
                BodyAssert(double.IsNaN(rows["a"]), "invalid weighted-list fallback remains unknown");
            }
            double calculated;
            BodyAssert(!TryEnemyImplantInstallProbabilityWithSockets(0.5, 0.25, 1, 1, 0, 1, 1, out calculated), "reject inconsistent probability inputs");
            BodyAssert(!TryEnemyImplantInstallProbabilityWithSockets(0.5, 1.0, 1, 1, 0, 1, 1000000, out calculated), "bounded work on modded roll counts");
            for (int minSockets = 0; minSockets < 4; minSockets++)
                for (int maxSockets = minSockets; maxSockets < 5; maxSockets++)
                    for (int granted = 0; granted < 4; granted++)
                    {
                        double expected = 0.0;
                        for (int sockets = minSockets; sockets <= maxSockets; sockets++)
                            for (int count = 0; count <= 5; count++)
                                expected += EnumerateInstallSequences(new int[count], 0, Math.Max(0, sockets - granted),
                                    new[] { 0.25, 0.5, 0.25 });
                        expected /= (maxSockets - minSockets + 1) * 6;
                        BodyAssert(TryEnemyImplantInstallProbabilityWithSockets(0.25, 0.75, minSockets,
                            maxSockets, granted, 0, 5, out calculated), "uniform socket range accepted");
                        BodyNear(calculated, expected, "uniform socket range oracle");
                    }
            BodyAssert(TryEnemyImplantInstallProbabilityWithSockets(1.0, 1.0, 0, int.MaxValue, 0, 1, 1, out calculated),
                "extreme socket range takes bounded work");
            BodyNear(calculated, (double)int.MaxValue / ((double)int.MaxValue + 1.0), "extreme capacity normalization does not overflow");
            foreach (double target in new[] { 0.0, 0.1, 0.25, 0.5, 1.0 })
                foreach (double competitor in new[] { 0.0, 0.1, 0.25, 0.5 })
                {
                    if (target + competitor > 1.0) continue;
                    for (int capacity = 0; capacity <= 4; capacity++)
                        for (int max = 0; max <= 6; max++)
                            for (int min = 0; min <= max; min++)
                            {
                                double expected = 0;
                                for (int count = min; count <= max; count++)
                                    expected += EnumerateInstallSequences(new int[count], 0, capacity,
                                        new[] { target, competitor, 1.0 - target - competitor });
                                expected /= max - min + 1;
                                BodyAssert(TryEnemyImplantInstallProbabilityWithSockets(target, target + competitor,
                                    capacity, capacity, 0, min, max, out calculated), "oracle input accepted");
                                BodyNear(calculated, expected, "exhaustive sequence oracle");
                            }
                }
            RunEnemyBodyIntegrationCases();
            return bodyChecks;
        }

        private static void ResetBodyFixture()
        {
            ResetEnemyBodySources(); LootEnemySourcesByItem.Clear(); KnownItemIds.Clear();
            Data.Items.Records.Clear(); Data.WoundSlots.Values.Clear(); Data.BodyTypes.Values.Clear();
        }
        private static void AddFixtureItem(ItemRecord item, string compositeId = null)
        {
            Data.Items.Records.Add(new CompositeItemRecord { Id = compositeId ?? item.Id, PrimaryRecord = item });
            KnownItemIds.Add(item.Id);
        }
        private static void AddFixtureSlot(string id, string type, string nature, int sockets)
        {
            Data.WoundSlots.Values.Add(new WoundSlotRecord { Id = id, SlotType = type, NatureType = nature,
                ImplantSocketsDefault = sockets, ImplantSocketsMin = sockets, ImplantSocketsMax = sockets });
        }
        private static void RunEnemyBodyIntegrationCases()
        {
            ResetBodyFixture();
            AddFixtureSlot("flesh_head", "head", "flesh", 1);
            AddFixtureSlot("metal_head", "head", "metal", 1);
            AddFixtureSlot("tail", "tail", "flesh", 1);
            Data.BodyTypes.Values.Add(new BodyTypeRecord { Id = "human", WoundSlots = new List<string> { "flesh_head" } });
            Data.BodyTypes.Values.Add(new BodyTypeRecord { Id = "robot", WoundSlots = new List<string> { "metal_head" } });
            AddFixtureItem(new ImplantRecord { Id = "a", SlotType = "head", NatureTypes = new List<string> { "flesh" }, AugmentationClass = AugmentationClass.Quasi });
            AddFixtureItem(new ImplantRecord { Id = "tail_implant", SlotType = "tail", NatureTypes = new List<string> { "flesh" }, AugmentationClass = AugmentationClass.Quasi });
            AddFixtureItem(new ImplantRecord { Id = "bad_nature", SlotType = "head", NatureTypes = new List<string> { "metal" }, AugmentationClass = AugmentationClass.Quasi });
            AddFixtureItem(new ImplantRecord { Id = "a_custom_variant", SlotType = "head", NatureTypes = new List<string> { "flesh" }, AugmentationClass = AugmentationClass.Quasi });
            AddFixtureItem(new ImplantRecord { Id = "high_tech", TechLevel = 8, SlotType = "head", NatureTypes = new List<string> { "flesh" }, AugmentationClass = AugmentationClass.Quasi });
            var mob = new MobClassRecord { BodyTypes = new List<string> { "human" }, ImplantCount = new IntRange { Min = 1, Max = 1 } };
            mob.ImplantClasses.Add(AugmentationClass.Quasi, 1.0f);
            var contexts = new List<EnemyLootContext> { new EnemyLootContext("pirates", 1, 1) };
            int slices = 0;
            while (!TickEnemyBodySources("psycho_fixture", mob, contexts)) { if (++slices > 1000) throw new Exception("iterator stuck"); }
            BodyAssert(slices > 0, "runtime pipeline yields");
            BodyNear(LootEnemySourcesByItem["a"][0].MaxPercent, 50.0, "typed runtime selection + successful installation");
            BodyAssert(!LootEnemySourcesByItem.ContainsKey("tail_implant") && !LootEnemySourcesByItem.ContainsKey("bad_nature"), "runtime excludes absent slot and incompatible nature");
            BodyAssert(!LootEnemySourcesByItem.ContainsKey("a_custom_variant") && !LootEnemySourcesByItem.ContainsKey("high_tech"), "runtime respects exact Randomize custom and tech filters");
            mob.ItemCategoriesWhitelist = new Dictionary<string, float>();
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("empty_whitelist_fixture", mob, contexts)) {}
            BodyAssert(LootEnemySourcesByItem.Count == 0, "non-null empty whitelist rejects all random candidates");
            mob.ItemCategoriesWhitelist.Add("Faction", 2.0f);
            Data.Items.GetSimpleRecord<ImplantRecord>("a", true).Categories.Add("pirates");
            ResetEnemyBodySources();
            while (!TickEnemyBodySources("faction_fixture", mob, contexts)) {}
            BodyNear(LootEnemySourcesByItem["a"][0].MaxPercent, 100.0, "matching spawn faction admits Quasi implant on humanoid");
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("case_fixture", mob, new List<EnemyLootContext> { new EnemyLootContext("Pirates", 1, 1) })) {}
            BodyAssert(LootEnemySourcesByItem.Count == 0, "category/faction case is not silently broadened");
            mob.ItemCategoriesWhitelist = null;
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("tech_range_fixture", mob, new List<EnemyLootContext> {
                contexts[0], new EnemyLootContext("pirates", 8, 8) })) {}
            BodyAssert(Math.Abs(LootEnemySourcesByItem["a"][0].MinPercent - 100f / 3f) < 0.00001f &&
                LootEnemySourcesByItem["a"][0].MaxPercent == 50f, "context range uses context-specific denominators");
            BodyAssert(LootEnemySourcesByItem["high_tech"][0].MinTech == 8 &&
                LootEnemySourcesByItem["high_tech"][0].MinPercent == 0, "absent lower-tech context contributes zero and earliest tech stays correct");
            LootEnemySourcesByItem.Clear();
            mob.BodyTypes.Add("robot");
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("mixed_fixture", mob, contexts)) {}
            BodyNear(LootEnemySourcesByItem["a"][0].MaxPercent, 25.0, "body variants averaged including incompatible variant");
            BodyNear(LootEnemySourcesByItem["bad_nature"][0].MaxPercent, 25.0, "second body has its own nature constraints");
            mob.BodyTypes.Remove("robot");
            AddFixtureItem(new AugmentationRecord { Id = "add_tail", WoundSlotIds = new List<string> { "tail" }, AugmentationClass = AugmentationClass.Quasi });
            ResetEnemyBodySources(); LootEnemySourcesByItem.Clear();
            mob.AugmentationClasses.Add(AugmentationClass.Quasi, 1.0f); mob.AugCount = new IntRange { Min = 1, Max = 1 };
            while (!TickEnemyBodySources("conditional_fixture", mob, contexts)) {}
            BodyAssert(float.IsNaN(LootEnemySourcesByItem["tail_implant"][0].MaxPercent), "runtime retains conditional augmentation-created slot");
            BodyAssert(float.IsNaN(LootEnemySourcesByItem["add_tail"][0].MaxPercent), "runtime does not claim exact random augmentation presence");
            BodyAssert(FormatEnemyLootChance(LootEnemySourcesByItem["tail_implant"][0], -1) == "ui.body_dependent", "conditional UI never prints NaN or fake percentage");
            mob.AugCount = new IntRange(); mob.GrantedImplants.Add("a"); mob.GrantedImplants.Add("a");
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("granted_fixture", mob, contexts)) {}
            BodyAssert(LootEnemySourcesByItem["a"].Count == 1, "duplicate grants consolidate and consume capacity before random rolls");
            BodyNear(LootEnemySourcesByItem["a"][0].MaxPercent, 100.0, "one successful grant remains exact");
            mob.GrantedImplants.Clear(); mob.AugmentationClasses.Clear();
            string oldHash = _compatAssemblySha256;
            _compatAssemblySha256 = "unsupported-build";
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("unsupported_fixture", mob, contexts)) {}
            BodyAssert(float.IsNaN(LootEnemySourcesByItem["a"][0].MaxPercent), "unreviewed assembly cannot inherit numerical confidence");
            _compatAssemblySha256 = oldHash;
            mob.BodyTypes[0] = "missing-body";
            LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("missing_body_fixture", mob, contexts)) {}
            BodyAssert(float.IsNaN(LootEnemySourcesByItem["a"][0].MaxPercent), "missing runtime body data stays conditional");
            mob.BodyTypes[0] = "human";
            AddFixtureItem(new ImplantRecord { Id = "none_class_item", SlotType = "head", NatureTypes = new List<string> { "flesh" }, AugmentationClass = AugmentationClass.None });
            mob.ImplantClasses.Add(AugmentationClass.None, 1.0f);
            ResetEnemyBodySources(); LootEnemySourcesByItem.Clear();
            while (!TickEnemyBodySources("none_fixture", mob, contexts)) {}
            BodyAssert(Math.Abs(LootEnemySourcesByItem["none_class_item"][0].MaxPercent - 100f / 6f) < 0.00001f,
                "None gates the roll but remains a valid candidate class when present in the dictionary");
            BodyAssert(Math.Abs(LootEnemySourcesByItem["a"][0].MaxPercent - 100f / 6f) < 0.00001f,
                "None-class candidate consumes its share of the denominator");
            ResetEnemyBodySources();
            BodyAssert(EnemyBodyCandidates.Count == 0 && EnemyImplantRules.Count == 0 && _enemyBodySourceWork == null, "lifecycle clears definitions and iterator");
        }
    }
}
