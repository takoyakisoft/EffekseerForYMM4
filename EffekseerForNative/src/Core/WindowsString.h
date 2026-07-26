#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>

#include <algorithm>
#include <limits>
#include <string>
#include <utility>

namespace EffekseerForYMM4::WindowsString
{
    inline std::wstring Utf8ToWide(const char* utf8)
    {
        if (utf8 == nullptr || *utf8 == '\0')
        {
            return {};
        }

        const auto sourceLength = std::char_traits<char>::length(utf8);
        if (sourceLength > static_cast<size_t>((std::numeric_limits<int>::max)()))
        {
            return {};
        }

        const auto wideLength = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            utf8,
            static_cast<int>(sourceLength),
            nullptr,
            0);
        if (wideLength <= 0)
        {
            return {};
        }

        std::wstring wide(static_cast<size_t>(wideLength), L'\0');
        if (MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                utf8,
                static_cast<int>(sourceLength),
                wide.data(),
                wideLength) <= 0)
        {
            return {};
        }

        return wide;
    }

    inline std::string WideToUtf8(const std::wstring& wide)
    {
        if (wide.empty() ||
            wide.size() > static_cast<size_t>((std::numeric_limits<int>::max)()))
        {
            return {};
        }

        const auto utf8Length = WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            wide.data(),
            static_cast<int>(wide.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (utf8Length <= 0)
        {
            return {};
        }

        std::string utf8(static_cast<size_t>(utf8Length), '\0');
        if (WideCharToMultiByte(
                CP_UTF8,
                WC_ERR_INVALID_CHARS,
                wide.data(),
                static_cast<int>(wide.size()),
                utf8.data(),
                utf8Length,
                nullptr,
                nullptr) <= 0)
        {
            return {};
        }

        return utf8;
    }

    inline bool StartsWith(const std::wstring& value, const wchar_t* prefix)
    {
        const auto prefixLength = std::char_traits<wchar_t>::length(prefix);
        return value.size() >= prefixLength &&
            std::equal(prefix, prefix + prefixLength, value.begin());
    }

    inline bool HasWin32DevicePrefix(const std::wstring& path)
    {
        return StartsWith(path, L"\\\\?\\") || StartsWith(path, L"\\\\.\\");
    }

    inline bool IsUncPath(const std::wstring& path)
    {
        return path.size() >= 2 &&
            path[0] == L'\\' &&
            path[1] == L'\\' &&
            !HasWin32DevicePrefix(path);
    }

    inline std::wstring ToAbsolutePath(std::wstring path)
    {
        std::replace(path.begin(), path.end(), L'/', L'\\');
        if (path.empty() || HasWin32DevicePrefix(path))
        {
            return path;
        }

        const auto required = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
        if (required == 0)
        {
            return {};
        }

        std::wstring absolute(static_cast<size_t>(required), L'\0');
        const auto written = GetFullPathNameW(
            path.c_str(),
            required,
            absolute.data(),
            nullptr);
        if (written == 0 || written >= required)
        {
            return {};
        }
        absolute.resize(written);
        return absolute;
    }

    inline std::wstring ToWin32ApiPath(std::wstring path)
    {
        auto absolute = ToAbsolutePath(std::move(path));
        if (absolute.empty() || HasWin32DevicePrefix(absolute))
        {
            return absolute;
        }

        if (IsUncPath(absolute))
        {
            return L"\\\\?\\UNC\\" + absolute.substr(2);
        }

        if (absolute.size() >= 3 &&
            absolute[1] == L':' &&
            absolute[2] == L'\\')
        {
            return L"\\\\?\\" + absolute;
        }

        return absolute;
    }

    inline std::u16string GetParentUtf16Path(const char16_t* path)
    {
        if (path == nullptr || *path == u'\0')
        {
            return {};
        }

        std::u16string value(path);
        const auto separator = value.find_last_of(u"/\\");
        return separator == std::u16string::npos
            ? std::u16string{}
            : value.substr(0, separator);
    }

    inline std::u16string CombineUtf16Path(const char16_t* basePath, const char16_t* relativePath)
    {
        std::u16string combined;
        if (basePath != nullptr)
        {
            combined.assign(basePath);
        }

        if (!combined.empty() && combined.back() != u'/' && combined.back() != u'\\')
        {
            combined.push_back(u'/');
        }

        if (relativePath != nullptr)
        {
            combined.append(relativePath);
        }

        std::replace(combined.begin(), combined.end(), u'\\', u'/');
        return combined;
    }

    inline std::wstring Utf8ToAbsolutePath(const char* pathUtf8)
    {
        return ToAbsolutePath(Utf8ToWide(pathUtf8));
    }
}
