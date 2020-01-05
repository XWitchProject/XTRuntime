using System;
namespace XTRuntime {
    public class XTRuntimeException : Exception {
        public static string ResultToMessage(LuaResult r) {
            switch(r) {
            case LuaResult.ErrErr: return "Error in error handler";
            case LuaResult.ErrMem: return "Out of memory";
            case LuaResult.ErrRun: return "Runtime error";
            case LuaResult.ErrSyntax: return "Syntax error";
            default: return "?";
            }
        }

        public XTRuntimeException(string msg) : base(msg) {}
    }
}
