namespace ItemIntelligence
{
    public static class ReflectionCases
    {
        private static int checks;
        private static void Check(bool value, string reason)
        { checks++; if (!value) throw new Exception("Reflection reader: " + reason); }

        private class RecordBase { public int Amount = 99; }
        private sealed class Record : RecordBase
        {
            public new int Amount { get; set; }
            private string _name = "hidden";
            private string Secret { get { return _name; } }
            public int AutoValue { get; set; }
            public int WriteOnly { set { Amount = value; } }
            public int this[int index] { get { throw new Exception("Indexer must not run"); } }
            public int Broken { get { throw new Exception("Getter fixture failure"); } }
            public static int Shared { get; set; }
            public bool Answer;
            private bool IsKnown(string id) { return Answer && id == _name; }
            private bool EnumKey(KeyKind id) { return id == KeyKind.Ready; }
            private bool Number(int id) { return id == 23; }
            private bool DecimalNumber(decimal id) { return id == 1.5m; }
            private bool Constructed(Key id) { return id != null && id.Value == _name; }
            private bool ObjectKey(object id) { return Equals(id, _name); }
            private bool Throws(string id) { throw new Exception(id); }
            private int NotBool(string id) { return id.Length; }
            private bool NoArgument() { return true; }
            public void Rename(string name) { _name = name; }
        }
        private enum KeyKind { Missing, Ready }
        private sealed class Key
        {
            public readonly string Value;
            public Key(string value) { Value = value; }
        }
        private sealed class CountingType : TypeDelegator
        {
            public int PropertyReads, FieldReads;
            public bool ThrowProperties, ThrowFields;
            public CountingType(Type type) : base(type) {}
            public override PropertyInfo[] GetProperties(BindingFlags flags)
            {
                PropertyReads++;
                if (ThrowProperties) throw new InvalidOperationException("Metadata fixture failure");
                return base.GetProperties(flags);
            }
            public override FieldInfo[] GetFields(BindingFlags flags)
            {
                FieldReads++;
                if (ThrowFields) throw new InvalidOperationException("Metadata fixture failure");
                return base.GetFields(flags);
            }
        }

        public static int Run()
        {
            checks = 0;
            ReflectionDataReader reader = new ReflectionDataReader();
            Record record = new Record { Amount = 7, AutoValue = 13, Answer = true };
            Check(Equals(reader.GetMember(record, "amount"), 7), "readable property wins over same-named base field");
            Check(Equals(reader.GetMember(record, "  __NAME  "), "hidden"), "private field aliases and whitespace");
            Check(Equals(reader.GetMember(record, "secret"), "hidden"), "private property lookup");
            Check(Equals(reader.GetMember(record, "<AutoValue>k__BackingField"), 13), "auto-property backing field lookup");
            Check(ReflectionDataReader.NormalizeMemberLookupName(" __<AutoValue>k__BackingField ") == "AutoValue", "backing alias normalization");
            Check(ReflectionDataReader.NormalizeMemberLookupName(null) == "", "null alias");
            Check(reader.GetMember(record, "Item") == null && reader.GetMember(record, "WriteOnly") == null,
                "indexed and write-only properties are excluded");
            Check(reader.GetMember(record, "Broken") == null, "throwing getter fails softly");
            Check(reader.GetMember(record, "missing") == null && reader.GetMember(null, "name") == null &&
                reader.GetMember(record, null) == null, "absent input fails softly");
            Record.Shared = 41;
            Check(Equals(reader.GetStaticMember(typeof(Record), "shared"), 41), "static property lookup");
            Check(reader.GetMember(record, "Shared") == null && reader.GetStaticMember(typeof(Record), "Amount") == null,
                "static and instance lookups remain separate");
            Check(reader.GetStaticMember(null, "Shared") == null && reader.GetStaticMember(typeof(Record), "") == null,
                "missing static input");
            Check(ReflectionDataReader.GetMemberValue(record, null) == null &&
                ReflectionDataReader.GetMemberValue(new object(), typeof(Record).GetProperty("Amount")) == null,
                "missing member or wrong receiver fails softly");
            for (int i = 0; i < 20; i++)
            {
                record.Amount = i; Record.Shared = i + 100; record.Rename("name" + i);
                Check(Equals(reader.GetMember(record, "Amount"), i) &&
                    Equals(reader.GetStaticMember(typeof(Record), "Shared"), i + 100) &&
                    Equals(reader.GetMember(record, "name"), "name" + i), "metadata cache never freezes live values");
            }
            Check(Equals(reader.GetMember(new Record { Amount = 88 }, "Amount"), 88), "instances do not share values");

            CountingType counted = new CountingType(typeof(Record));
            MemberInfo member = reader.FindCachedMember(counted, "Amount", false);
            for (int i = 0; i < 1000; i++)
            {
                Check(ReferenceEquals(reader.FindCachedMember(counted, "Amount", false), member), "warm metadata identity");
                Check(reader.FindCachedMember(counted, "Missing", false) == null, "warm negative lookup");
            }
            Check(counted.PropertyReads == 1 && counted.FieldReads == 1, "warm hit and miss avoid rescanning metadata");
            reader.FindCachedMember(counted, "Shared", true);
            Check(counted.PropertyReads == 2 && counted.FieldReads == 2, "static metadata has a separate lookup");
            ReflectionDataReader other = new ReflectionDataReader();
            other.FindCachedMember(counted, "Amount", false);
            Check(counted.PropertyReads == 3 && counted.FieldReads == 3, "reader owns its lookup cache");

            IList<MemberInfo> members = reader.GetReadableMembers(counted);
            Check(members.IsReadOnly && members.Count > 0, "readable metadata is an immutable snapshot");
            Check(ReferenceEquals(reader.GetReadableMembers(counted), members), "readable snapshot reused");
            Check(counted.PropertyReads == 4 && counted.FieldReads == 4, "readable cache scans once");
            other.GetReadableMembers(counted);
            Check(counted.PropertyReads == 5 && counted.FieldReads == 5, "reader owns its readable cache");
            foreach (MemberInfo value in members)
            {
                FieldInfo field = value as FieldInfo;
                PropertyInfo property = value as PropertyInfo;
                Check((field == null || !field.IsStatic) && (property == null ||
                    (property.CanRead && property.GetIndexParameters().Length == 0 && !property.GetGetMethod(true).IsStatic)),
                    "only readable non-indexed instance metadata enumerated");
            }
            int size = members.Count;
            bool addRejected = false, removeRejected = false;
            try { members.Add(member); } catch (NotSupportedException) { addRejected = true; }
            try { members.RemoveAt(0); } catch (NotSupportedException) { removeRejected = true; }
            Check(addRejected && removeRejected && reader.GetReadableMembers(counted).Count == size,
                "a caller cannot corrupt shared metadata");
            Check(reader.GetReadableMembers(null).Count == 0 && reader.GetReadableMembers(null).IsReadOnly, "empty snapshot immutable");
            CountingType failedProperties = new CountingType(typeof(Record)) { ThrowProperties = true };
            CountingType failedFields = new CountingType(typeof(Record)) { ThrowFields = true };
            Check(reader.FindCachedMember(failedProperties, "name", false) is FieldInfo, "property metadata failure preserves fields");
            Check(reader.FindCachedMember(failedFields, "Amount", false) is PropertyInfo, "field metadata failure preserves properties");
            Check(reader.GetReadableMembers(failedProperties).Count > 0 && reader.GetReadableMembers(failedFields).Count > 0,
                "readable enumeration preserves partial metadata");

            record.Rename("hidden");
            Check(reader.CallBool(record, "IsKnown", "hidden") == true, "private bool invocation");
            record.Answer = false;
            Check(reader.CallBool(record, "IsKnown", "hidden") == false, "warm method observes live receiver");
            Check(reader.CallBool(new Record { Answer = true }, "IsKnown", "hidden") == true, "method cache never retains previous receiver");
            Check(reader.CallBool(record, "EnumKey", "ready") == true && reader.CallBool(record, "EnumKey", "unknown") == null,
                "enum conversion is case-insensitive and fails softly");
            Check(reader.CallBool(record, "Number", "23") == true && reader.CallBool(record, "Number", "bad") == null,
                "numeric conversion rejects invalid value types");
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(reader.CallBool(record, "DecimalNumber", "1.5") == true, "numeric argument conversion uses invariant culture");
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Check(reader.CallBool(record, "Constructed", "hidden") == true && reader.CallBool(record, "ObjectKey", "hidden") == true,
                "string constructor and object arguments supported");
            foreach (string method in new string[] { "missing", "Throws", "NotBool", "NoArgument", null, "" })
            {
                Check(reader.CallBool(record, method, "hidden") == null, "inapplicable method fails softly");
                Check(reader.CallBool(record, method, "hidden") == null, "warm inapplicable method fails softly");
            }
            Check(reader.CallBool(null, "IsKnown", "hidden") == null && reader.CallBool(record, "IsKnown", "") == null,
                "empty method invocation input");
            return checks;
        }
    }
}
