#include <format>
#include <string>
#include "shared_memory.h"

namespace
{
    std::wstring GetWin32ErrorMessage()
    {
        const DWORD errorCode = GetLastError();
        if (errorCode == 0) return {};

        LPWSTR messageBuffer = nullptr;

        DWORD size = FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            NULL,
            errorCode,
            MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
            (LPWSTR)&messageBuffer,
            0,
            NULL
        );

        std::wstring resultMessage;
        if (size > 0 && messageBuffer != nullptr)
        {
            resultMessage = messageBuffer;
            if (!resultMessage.empty() && resultMessage.back() == L'\n') resultMessage.pop_back();
            if (!resultMessage.empty() && resultMessage.back() == L'\r') resultMessage.pop_back();
        }
        else
        {
            resultMessage = L"Unknown native error code: " + std::to_wstring(errorCode);
        }

        if (messageBuffer != nullptr)
        {
            LocalFree(messageBuffer);
        }

        return resultMessage;
    }
}

void SharedMemoryHandle::Close()
{
    if (data != nullptr) { UnmapViewOfFile(data); data = nullptr; }
    if (mapping != NULL) { CloseHandle(mapping); mapping = NULL; }
    buffer = nullptr;
    length = 0;
}

bool SharedMemoryV8Handler::Execute(
    const CefString& name,
    CefRefPtr<CefV8Value> object,
    const CefV8ValueList& arguments,
    CefRefPtr<CefV8Value>& retval,
    CefString& exception)
{
    if (name == "openMapping")
    {
        if (arguments.size() < 2 || !arguments[0]->IsString() || !arguments[1]->IsInt())
        {
            exception = "Invalid arguments. Expected a string and an integer.";
            return true;
        }

        std::wstring path = arguments[0]->GetStringValue();
        uint32_t length = arguments[1]->GetUIntValue();

        HANDLE mapping = OpenFileMapping(FILE_MAP_READ, FALSE, path.c_str());
        if (mapping == nullptr) {
            auto err = GetWin32ErrorMessage();
            exception = std::format(L"Could not open shared memory object \"{}\" (ERROR: {})", path, err);
            return true;
        }

        void* data = MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, length);
        if (data == nullptr) {
            auto err = GetWin32ErrorMessage();
            CloseHandle(mapping);
            exception = std::format(L"Could not map shared memory object \"{}\" (ERROR: {})", path, err);
            return true;
        }

        CefRefPtr<CefV8BackingStore> store = CefV8BackingStore::Create(length);
        CefRefPtr<CefV8Value> buffer = CefV8Value::CreateArrayBufferFromBackingStore(store);

        CefRefPtr<CefV8Value> obj = CefV8Value::CreateObject(nullptr, nullptr);
        obj->SetValue("name", arguments[0], V8_PROPERTY_ATTRIBUTE_NONE);
        obj->SetValue("length", arguments[1], V8_PROPERTY_ATTRIBUTE_NONE);
        obj->SetValue("buffer", buffer, V8_PROPERTY_ATTRIBUTE_NONE);
        obj->SetValue("requiresCopy", CefV8Value::CreateBool(true), V8_PROPERTY_ATTRIBUTE_NONE);
        obj->SetValue("copyFrom", CefV8Value::CreateFunction("copyFrom", this), V8_PROPERTY_ATTRIBUTE_NONE);
        obj->SetValue("close", CefV8Value::CreateFunction("close", this), V8_PROPERTY_ATTRIBUTE_NONE);

        CefRefPtr<SharedMemoryHandle> handle = new SharedMemoryHandle(mapping, data, buffer, length);
        obj->SetUserData(handle);

        retval = obj;
        return true;
    }

    if (name == "copyFrom")
    {
        if (object == nullptr || !object->IsObject())
        {
            exception = "Execution context missing object metadata.";
            return true;
        }

        CefRefPtr<CefBaseRefCounted> userData = object->GetUserData();
        if (userData != nullptr)
        {
            SharedMemoryHandle* handle = static_cast<SharedMemoryHandle*>(userData.get());
            void* ptr = handle->buffer->GetArrayBufferData();
            if (ptr != nullptr)
            {
                memcpy(ptr, handle->data, handle->length);
            }
        }

        return true;
    }

    if (name == "close")
    {
        if (object == nullptr || !object->IsObject())
        {
            exception = "Execution context missing object metadata.";
            return true;
        }

        CefRefPtr<CefBaseRefCounted> userData = object->GetUserData();
        if (userData != nullptr)
        {
            const auto handle = dynamic_cast<SharedMemoryHandle*>(userData.get());
            handle->Close();
            object->SetUserData(nullptr);
        }

        return true;
    }

    return false;
}