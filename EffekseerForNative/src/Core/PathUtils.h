#pragma once

#include <filesystem>
#include <optional>
#include <string>
#include <string_view>

namespace EffekseerForNative::PathUtils
{
    std::u16string ToUtf16PathString(const std::filesystem::path& path);

    bool Exists(const std::filesystem::path& path);
    std::filesystem::path Combine(const std::filesystem::path& basePath, const std::filesystem::path& childPath);
    bool EnsureDirectory(const std::filesystem::path& path, std::wstring* errorMessage = nullptr);
    bool WriteUtf8TextFile(const std::filesystem::path& path, std::string_view content, std::wstring* errorMessage = nullptr);
    bool ReadUtf8TextFile(const std::filesystem::path& path, std::string* content, std::wstring* errorMessage = nullptr);
}
