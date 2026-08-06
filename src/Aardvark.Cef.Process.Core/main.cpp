#include "shared_memory.h"

#define DllExport(t) extern "C" __declspec( dllexport ) t __cdecl

DllExport(void) onContextCreated(CefV8Context* pContext)
{
    pContext->Enter();

    CefRefPtr<CefV8Handler> sharedMemoryHandler = new SharedMemoryV8Handler();
    CefRefPtr<CefV8Value> openMapping = CefV8Value::CreateFunction("openMapping", sharedMemoryHandler);

    CefRefPtr<CefV8Value> aardvark = CefV8Value::CreateObject(nullptr, nullptr);
    aardvark->SetValue("openMapping", openMapping, V8_PROPERTY_ATTRIBUTE_NONE);

    CefRefPtr<CefV8Value> globalWindow = pContext->GetGlobal();
    globalWindow->SetValue("aardvark", aardvark, V8_PROPERTY_ATTRIBUTE_NONE);

    pContext->Exit();
}