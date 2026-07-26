#include "NativePathBridge.h"

#include "../Core/PathUtils.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <string>
#include <windows.h>

namespace
{
    thread_local std::string last_error;

    std::filesystem::path ToPath(const char* value_utf8)
    {
        return value_utf8 == nullptr
            ? std::filesystem::path{}
            : std::filesystem::path(std::u8string(reinterpret_cast<const char8_t*>(value_utf8)));
    }

    std::string ToUtf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const auto size = WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (size <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(size), '\0');
        WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            result.data(),
            size,
            nullptr,
            nullptr);
        return result;
    }

    std::string PathToUtf8(const std::filesystem::path& value)
    {
        const auto text = value.u8string();
        return {reinterpret_cast<const char*>(text.data()), text.size()};
    }

    int32_t CopyToBuffer(const std::string& value, char* buffer, int32_t buffer_size)
    {
        const auto required = static_cast<int32_t>(value.size() + 1);
        if (buffer == nullptr || buffer_size <= 0)
        {
            return required;
        }

        const auto copy_size = (std::min)(static_cast<size_t>(buffer_size - 1), value.size());
        if (copy_size > 0)
        {
            std::memcpy(buffer, value.data(), copy_size);
        }
        buffer[copy_size] = '\0';
        return required;
    }

    void SetLastError(const std::wstring& error)
    {
        last_error = ToUtf8(error);
    }
}

int32_t effekseer_path_combine(
    const char* base_path_utf8,
    const char* child_path_utf8,
    char* buffer,
    int32_t buffer_size)
{
    try
    {
        last_error.clear();
        return CopyToBuffer(
            PathToUtf8(EffekseerForNative::PathUtils::Combine(
                ToPath(base_path_utf8),
                ToPath(child_path_utf8))),
            buffer,
            buffer_size);
    }
    catch (...)
    {
        last_error = "Failed to combine paths.";
        return 0;
    }
}

int32_t effekseer_path_ensure_directory(const char* path_utf8)
{
    std::wstring error;
    const auto result = EffekseerForNative::PathUtils::EnsureDirectory(ToPath(path_utf8), &error);
    SetLastError(error);
    return result ? 1 : 0;
}

int32_t effekseer_path_exists(const char* path_utf8)
{
    last_error.clear();
    return EffekseerForNative::PathUtils::Exists(ToPath(path_utf8)) ? 1 : 0;
}

int32_t effekseer_path_write_utf8_text(const char* path_utf8, const char* content_utf8)
{
    std::wstring error;
    const auto result = EffekseerForNative::PathUtils::WriteUtf8TextFile(
        ToPath(path_utf8),
        content_utf8 == nullptr ? std::string_view{} : std::string_view(content_utf8),
        &error);
    SetLastError(error);
    return result ? 1 : 0;
}

int32_t effekseer_path_read_utf8_text(
    const char* path_utf8,
    char* buffer,
    int32_t buffer_size)
{
    std::wstring error;
    std::string content;
    if (!EffekseerForNative::PathUtils::ReadUtf8TextFile(ToPath(path_utf8), &content, &error))
    {
        SetLastError(error);
        return 0;
    }

    last_error.clear();
    return CopyToBuffer(content, buffer, buffer_size);
}

int32_t effekseer_path_get_last_error(char* buffer, int32_t buffer_size)
{
    return CopyToBuffer(last_error, buffer, buffer_size);
}
