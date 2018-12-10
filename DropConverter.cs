using System;
using System.Collections.Generic;
using Game.Utils.Strings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Utils.Json.Converters {
    public class DropConverter : JsonConverter {

        public override bool CanConvert(Type objectType) {
            throw new NotImplementedException();
        }

        public override bool CanWrite {
            get { return true; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            List<DungeonsManager.DropInLevel.Drop> result = new List<DungeonsManager.DropInLevel.Drop>();
            var original = serializer.Deserialize<List<JObject>>(reader);

            if (original != null) {
                foreach (var node in original) {
                    var item = ParseDrop(node);
                    if (item != null) {
                        result.Add(item);
                    }
                }
            }
            
            return result;
        }

        private DungeonsManager.DropInLevel.Drop ParseDrop(JObject node) {
            string dr = node.Get<string>(JsonConstants.DropType);
            if (dr == null)
                return null;

            DungeonsManager.DropInLevel.DropType type = EnumHelper.ParseEnum<DungeonsManager.DropInLevel.DropType>(dr);
            DungeonsManager.DropInLevel.Drop drop = null;
            switch (type) {
                case DungeonsManager.DropInLevel.DropType.Resource:
                    drop = node.ToObject<DungeonsManager.DropInLevel.DropResource>();
                    break;
                case DungeonsManager.DropInLevel.DropType.Monster:
                    drop = node.ToObject<DungeonsManager.DropInLevel.DropMonster>();
                    break;
                case DungeonsManager.DropInLevel.DropType.Item:
                    drop = node.ToObject<DungeonsManager.DropInLevel.DropItem>();
                    break;
                case DungeonsManager.DropInLevel.DropType.Rune:
                    drop = node.ToObject<DungeonsManager.DropInLevel.DropRune>();
                    break;
            }

            if(drop != null)
                drop.Init();

            return drop;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            serializer.Serialize(writer, value);
        }
    }
}
