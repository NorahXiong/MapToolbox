#include <string>

namespace unity_debug {
    typedef void (*LogFunc)(const char*);

    static LogFunc funcPtrLog = nullptr;
    static LogFunc funcPtrLogWarning = nullptr;
    static LogFunc funcPtrLogError = nullptr;

    void SetFuncPtrLog(LogFunc fp) { funcPtrLog = fp; }
    void SetFuncPtrLogWarning(LogFunc fp) { funcPtrLogWarning = fp; }
    void SetFuncPtrLogError(LogFunc fp) { funcPtrLogError = fp; }

    void Log(const char* msg) { if (funcPtrLog) funcPtrLog(msg); }
    void Log(std::string msg) { Log(msg.c_str()); }
    void LogWarning(const char* msg) { if (funcPtrLogWarning) funcPtrLogWarning(msg); }
    void LogWarning(std::string msg) { LogWarning(msg.c_str()); }
    void LogError(const char* msg) { if (funcPtrLogError) funcPtrLogError(msg); }
    void LogError(std::string msg) { LogError(msg.c_str()); }
}

extern "C" {
    void SetFunctionDebugLog(void (*fp)(const char*)) { unity_debug::SetFuncPtrLog(fp); }
    void SetFunctionDebugLogWarning(void (*fp)(const char*)) { unity_debug::SetFuncPtrLogWarning(fp); }
    void SetFunctionDebugLogError(void (*fp)(const char*)) { unity_debug::SetFuncPtrLogError(fp); }
}
