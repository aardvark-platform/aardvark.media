#pragma once
#include "include/cef_v8.h"

class SharedMemoryHandle : public CefBaseRefCounted
{
public:
    HANDLE mapping = nullptr;
    LPVOID data = nullptr;
    CefRefPtr<CefV8Value> buffer = nullptr;
    uint32_t length = 0;

    SharedMemoryHandle(HANDLE mapping, LPVOID data, CefRefPtr<CefV8Value> buffer, uint32_t length) : mapping(mapping), data(data), buffer(buffer), length(length) {}
    ~SharedMemoryHandle() override { Close(); }

    void Close();

private:
    IMPLEMENT_REFCOUNTING(SharedMemoryHandle);
};

class SharedMemoryV8Handler : public CefV8Handler
{
public:
    SharedMemoryV8Handler() = default;

    bool Execute(
        const CefString& name,
        CefRefPtr<CefV8Value> object,
        const CefV8ValueList& arguments,
        CefRefPtr<CefV8Value>& retval,
        CefString& exception) override;

private:
    IMPLEMENT_REFCOUNTING(SharedMemoryV8Handler);
};