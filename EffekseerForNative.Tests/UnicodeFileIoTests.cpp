#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>

#include "Effekseer.DefaultFile.h"

#include <array>
#include <iostream>
#include <string>
#include <vector>

namespace
{
    std::wstring ToExtendedPath(const std::wstring& path)
    {
        if (path.rfind(L"\\\\?\\", 0) == 0)
        {
            return path;
        }
        if (path.rfind(L"\\\\", 0) == 0)
        {
            return L"\\\\?\\UNC\\" + path.substr(2);
        }
        return L"\\\\?\\" + path;
    }

    bool CreateDirectoryForTest(const std::wstring& path)
    {
        if (CreateDirectoryW(ToExtendedPath(path).c_str(), nullptr) != FALSE)
        {
            return true;
        }
        return GetLastError() == ERROR_ALREADY_EXISTS;
    }
}

int wmain()
{
    std::array<wchar_t, 32768> tempPathBuffer{};
    const auto tempPathLength = GetTempPathW(
        static_cast<DWORD>(tempPathBuffer.size()),
        tempPathBuffer.data());
    if (tempPathLength == 0 || tempPathLength >= tempPathBuffer.size())
    {
        std::cerr << "GetTempPathW failed." << std::endl;
        return 1;
    }

    std::wstring currentPath(tempPathBuffer.data(), tempPathLength);
    currentPath += L"EffekseerForYMM4_UnicodeIo_" + std::to_wstring(GetCurrentProcessId());

    std::vector<std::wstring> createdDirectories;
    auto cleanup = [&]()
    {
        for (auto it = createdDirectories.rbegin(); it != createdDirectories.rend(); ++it)
        {
            RemoveDirectoryW(ToExtendedPath(*it).c_str());
        }
    };

    if (!CreateDirectoryForTest(currentPath))
    {
        std::cerr << "Failed to create test root." << std::endl;
        return 1;
    }
    createdDirectories.push_back(currentPath);

    while (currentPath.size() < 280)
    {
        currentPath += L"\\日本語_emoji_\U0001F680_segment";
        if (!CreateDirectoryForTest(currentPath))
        {
            std::cerr << "Failed to create long Unicode directory." << std::endl;
            cleanup();
            return 1;
        }
        createdDirectories.push_back(currentPath);
    }

    const auto filePath = currentPath + L"\\実ファイル_\U0001F60A.bin";
    const auto extendedFilePath = ToExtendedPath(filePath);
    const auto fileHandle = CreateFileW(
        extendedFilePath.c_str(),
        GENERIC_WRITE,
        FILE_SHARE_READ,
        nullptr,
        CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (fileHandle == INVALID_HANDLE_VALUE)
    {
        std::cerr << "Failed to create long Unicode test file." << std::endl;
        cleanup();
        return 1;
    }

    constexpr std::array<char, 23> expected = {
        'e', 'f', 'f', 'e', 'k', 's', 'e', 'e', 'r', '-', 'l', 'o', 'n', 'g', '-', 'p', 'a', 't', 'h', '-', 'i', 'o', '!'};
    DWORD bytesWritten = 0;
    const auto writeSucceeded = WriteFile(
        fileHandle,
        expected.data(),
        static_cast<DWORD>(expected.size()),
        &bytesWritten,
        nullptr);
    CloseHandle(fileHandle);
    if (writeSucceeded == FALSE || bytesWritten != expected.size())
    {
        std::cerr << "Failed to write long Unicode test file." << std::endl;
        DeleteFileW(extendedFilePath.c_str());
        cleanup();
        return 1;
    }

    static_assert(sizeof(wchar_t) == sizeof(char16_t));
    Effekseer::DefaultFileInterface fileInterface;
    auto reader = fileInterface.OpenRead(
        reinterpret_cast<const char16_t*>(filePath.c_str()));
    if (reader == nullptr)
    {
        std::cerr << "DefaultFileInterface failed to open a >260-character Unicode path." << std::endl;
        DeleteFileW(extendedFilePath.c_str());
        cleanup();
        return 1;
    }

    std::array<char, expected.size()> actual{};
    const auto bytesRead = reader->Read(actual.data(), actual.size());
    reader.Reset();

    DeleteFileW(extendedFilePath.c_str());
    cleanup();

    if (bytesRead != expected.size() || actual != expected)
    {
        std::cerr << "DefaultFileInterface returned unexpected file contents." << std::endl;
        return 1;
    }

    std::cout << "Unicode long-path file I/O passed." << std::endl;
    return 0;
}
