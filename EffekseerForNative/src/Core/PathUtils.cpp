#include "PathUtils.h"

#include <cstdio>
#include <system_error>
#include <vector>

namespace
{
    std::wstring BuildErrorMessage(const std::wstring& action, const std::filesystem::path& path, const std::error_code& error)
    {
        std::wstring message = action;
        message += L": ";
        message += path.native();
        if (error)
        {
            message += L" (error=";
            message += std::to_wstring(error.value());
            message += L")";
        }
        return message;
    }

    std::wstring BuildErrnoMessage(const std::wstring& action, const std::filesystem::path& path, errno_t error)
    {
        std::error_code ec(error, std::generic_category());
        return BuildErrorMessage(action, path, ec);
    }
}

namespace EffekseerForNative::PathUtils
{
    std::u16string ToUtf16PathString(const std::filesystem::path& path)
    {
        static_assert(sizeof(std::filesystem::path::value_type) == sizeof(char16_t), "Windows wide paths must be UTF-16.");
        const auto native = path.native();
        return std::u16string(native.begin(), native.end());
    }

    bool Exists(const std::filesystem::path& path)
    {
        std::error_code error;
        return std::filesystem::exists(path, error);
    }

    std::filesystem::path Combine(const std::filesystem::path& basePath, const std::filesystem::path& childPath)
    {
        return basePath / childPath;
    }

    bool EnsureDirectory(const std::filesystem::path& path, std::wstring* errorMessage)
    {
        std::error_code error;
        if (std::filesystem::exists(path, error))
        {
            if (std::filesystem::is_directory(path, error))
            {
                return true;
            }

            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrorMessage(L"Path exists but is not a directory", path, error);
            }
            return false;
        }

        error.clear();
        if (std::filesystem::create_directories(path, error) || std::filesystem::exists(path, error))
        {
            return true;
        }

        if (errorMessage != nullptr)
        {
            *errorMessage = BuildErrorMessage(L"Failed to create directory", path, error);
        }
        return false;
    }

    bool WriteUtf8TextFile(const std::filesystem::path& path, std::string_view content, std::wstring* errorMessage)
    {
        if (path.has_parent_path())
        {
            if (!EnsureDirectory(path.parent_path(), errorMessage))
            {
                return false;
            }
        }

        FILE* file = nullptr;
        const auto widePath = path.native();
        const auto openError = _wfopen_s(&file, widePath.c_str(), L"wb");
        if (openError != 0 || file == nullptr)
        {
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrnoMessage(L"Failed to open file for write", path, openError);
            }
            return false;
        }

        const auto bytesWritten = fwrite(content.data(), 1, content.size(), file);
        fclose(file);

        if (bytesWritten != content.size())
        {
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrorMessage(L"Failed to write complete file", path, {});
            }
            return false;
        }

        return true;
    }

    bool ReadUtf8TextFile(const std::filesystem::path& path, std::string* content, std::wstring* errorMessage)
    {
        if (content == nullptr)
        {
            if (errorMessage != nullptr)
            {
                *errorMessage = L"ReadUtf8TextFile requires a non-null output buffer.";
            }
            return false;
        }

        FILE* file = nullptr;
        const auto widePath = path.native();
        const auto openError = _wfopen_s(&file, widePath.c_str(), L"rb");
        if (openError != 0 || file == nullptr)
        {
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrnoMessage(L"Failed to open file for read", path, openError);
            }
            return false;
        }

        if (_fseeki64(file, 0, SEEK_END) != 0)
        {
            fclose(file);
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrorMessage(L"Failed to seek file end", path, {});
            }
            return false;
        }

        const auto size = _ftelli64(file);
        if (size < 0 || _fseeki64(file, 0, SEEK_SET) != 0)
        {
            fclose(file);
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrorMessage(L"Failed to seek file start", path, {});
            }
            return false;
        }

        std::vector<char> buffer(static_cast<size_t>(size));
        const auto bytesRead = buffer.empty() ? 0 : fread(buffer.data(), 1, buffer.size(), file);
        fclose(file);

        if (bytesRead != buffer.size())
        {
            if (errorMessage != nullptr)
            {
                *errorMessage = BuildErrorMessage(L"Failed to read complete file", path, {});
            }
            return false;
        }

        content->assign(buffer.begin(), buffer.end());
        return true;
    }
}
