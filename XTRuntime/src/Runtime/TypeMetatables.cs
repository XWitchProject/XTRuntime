using System;
using System.Collections.Generic;
using System.Reflection;

namespace XTRuntime {
    public partial class XTRuntime {
        public delegate int SpecialNodeIndexFunc(object obj);
        public delegate int SpecialNodeNewIndexFunc(object obj, object value);

        public Dictionary<string, Type> MetatableMap = new Dictionary<string, Type>();
        public Dictionary<Type, string> ReverseMetatableMap = new Dictionary<Type, string>();

        public Dictionary<Type, Dictionary<string, SpecialNodeIndexFunc>> SpecialIndexMap = new Dictionary<Type, Dictionary<string, SpecialNodeIndexFunc>>();
        public Dictionary<Type, Dictionary<string, SpecialNodeNewIndexFunc>> SpecialNewIndexMap = new Dictionary<Type, Dictionary<string, SpecialNodeNewIndexFunc>>();

        public Dictionary<Type, Dictionary<string, FieldInfo>> TypeFieldMap = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        public Dictionary<Type, MethodInfo> ContainerIndexMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerNewIndexMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerCountMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerAddMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerRemoveMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ListInsertMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerClearMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> ContainerContainsMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> DictGetEnumeratorMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> DictEnumeratorCurrentMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> DictEnumeratorMoveNextMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> DictEnumeratorKVPairKeyMethodMap = new Dictionary<Type, MethodInfo>();
        public Dictionary<Type, MethodInfo> DictEnumeratorKVPairValueMethodMap = new Dictionary<Type, MethodInfo>();


        private void CreateListMethodMap(Type type) {
            ContainerIndexMethodMap[type] = type.GetMethod("get_Item");
            ContainerNewIndexMethodMap[type] = type.GetMethod("set_Item");
            ContainerCountMethodMap[type] = type.GetMethod("get_Count");
            ContainerAddMethodMap[type] = type.GetMethod("Add");
            ContainerRemoveMethodMap[type] = type.GetMethod("RemoveAt");
            ListInsertMethodMap[type] = type.GetMethod("Insert");
            ContainerClearMethodMap[type] = type.GetMethod("Clear");
            ContainerContainsMethodMap[type] = type.GetMethod("Contains");
        }

        private void CreateDictionaryMethodMap(Type type) {
            ContainerIndexMethodMap[type] = type.GetMethod("get_Item");
            ContainerNewIndexMethodMap[type] = type.GetMethod("set_Item");
            ContainerCountMethodMap[type] = type.GetMethod("get_Count");
            ContainerRemoveMethodMap[type] = type.GetMethod("Remove", new Type[] { type.GetGenericArguments()[0] });
            ContainerClearMethodMap[type] = type.GetMethod("Clear");
            ContainerContainsMethodMap[type] = type.GetMethod("ContainsKey");
            var enum_method = type.GetMethod("GetEnumerator");
            DictGetEnumeratorMethodMap[type] = enum_method;
            var enum_current_method = enum_method.ReturnType.GetMethod("get_Current");
            DictEnumeratorCurrentMethodMap[type] = enum_current_method;
            DictEnumeratorMoveNextMethodMap[type] = enum_method.ReturnType.GetMethod("MoveNext");
            var kv_pair_type = enum_current_method.ReturnType;
            DictEnumeratorKVPairKeyMethodMap[type] = kv_pair_type.GetMethod("get_Key", BindingFlags.Public | BindingFlags.Instance);
            DictEnumeratorKVPairValueMethodMap[type] = kv_pair_type.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance);
        }


        private void CreateGenericMetamethods() {
            Lua.lua_pushcfunction(LuaStatePtr, LuaObjectFinalizer);
            Lua.lua_setfield(LuaStatePtr, -2, "__gc");
            Lua.lua_pushcfunction(LuaStatePtr, LuaObjectToString);
            Lua.lua_setfield(LuaStatePtr, -2, "__tostring");
        }

        private void CreateListMetatable(Type type, string name) {
            MetatableMap[name] = type;
            ReverseMetatableMap[type] = name;
            Lua.luaL_newmetatable(LuaStatePtr, name);
            CreateGenericMetamethods();
            Lua.lua_pushcfunction(LuaStatePtr, LuaListIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__index");
            Lua.lua_pushcfunction(LuaStatePtr, LuaListNewIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__newindex");
        }

        private void CreateDictionaryMetatable(Type type, string name) {
            MetatableMap[name] = type;
            ReverseMetatableMap[type] = name;
            Lua.luaL_newmetatable(LuaStatePtr, name);
            CreateGenericMetamethods();
            Lua.lua_pushcfunction(LuaStatePtr, LuaDictIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__index");
            Lua.lua_pushcfunction(LuaStatePtr, LuaDictNewIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__newindex");
        }

        private void CreateNodeMetatable(Type type, string name) {
            MetatableMap[name] = type;
            ReverseMetatableMap[type] = name;
            Lua.luaL_newmetatable(LuaStatePtr, name);
            CreateGenericMetamethods();

            var map = TypeFieldMap[type] = new Dictionary<string, FieldInfo>();
            var fields = type.GetFields();
            for (var i = 0; i < fields.Length; i++) {
                var field = fields[i];
                map[field.Name] = field;
            }

            Lua.lua_pushcfunction(LuaStatePtr, LuaNodeIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__index");
            Lua.lua_pushcfunction(LuaStatePtr, LuaNodeNewIndex);
            Lua.lua_setfield(LuaStatePtr, -2, "__newindex");
        }

        private string GetTypeMetatable(Type type) {
            if (!ReverseMetatableMap.TryGetValue(type, out string name)) {
                throw new Exception($"Unsupported type: '{type}'");
            }
            return name;
        }

        public void RegisterSpecialIndexFunc(Type type, string name, SpecialNodeIndexFunc func) {
            Dictionary<string, SpecialNodeIndexFunc> map;
            if (!SpecialIndexMap.TryGetValue(type, out map)) {
                SpecialIndexMap[type] = map = new Dictionary<string, SpecialNodeIndexFunc>();
            }
            map[name] = func;
        }

        public SpecialNodeIndexFunc GetSpecialIndexFunc(Type type, string name) {
            if (!SpecialIndexMap.TryGetValue(type, out Dictionary<string, SpecialNodeIndexFunc> map)) {
                return null;
            }
            if (!map.TryGetValue(name, out SpecialNodeIndexFunc result)) return null;
            return result;
        }

        public void RegisterSpecialNewIndexFunc(Type type, string name, SpecialNodeNewIndexFunc func) {
            Dictionary<string, SpecialNodeNewIndexFunc> map;
            if (!SpecialNewIndexMap.TryGetValue(type, out map))
            {
                SpecialNewIndexMap[type] = map = new Dictionary<string, SpecialNodeNewIndexFunc>();
            }
            map[name] = func;
        }

        public SpecialNodeNewIndexFunc GetSpecialNewIndexFunc(Type type, string name) {
            if (!SpecialNewIndexMap.TryGetValue(type, out Dictionary<string, SpecialNodeNewIndexFunc> map))
            {
                return null;
            }
            if (!map.TryGetValue(name, out SpecialNodeNewIndexFunc result)) return null;
            return result;
        }
    }
}
