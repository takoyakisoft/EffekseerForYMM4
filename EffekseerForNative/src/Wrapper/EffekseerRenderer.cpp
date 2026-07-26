#include "EffekseerRenderer.h"

#include "../Core/EffectsManager.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <new>
#include <string>
#include <windows.h>

namespace
{
    EffectsManager* GetManager(EffekseerRendererHandle handle)
    {
        return static_cast<EffectsManager*>(handle);
    }

    std::filesystem::path ToPath(const char* pathUtf8)
    {
        if (pathUtf8 == nullptr || *pathUtf8 == '\0')
        {
            return {};
        }

        return std::filesystem::path(
            std::u8string(reinterpret_cast<const char8_t*>(pathUtf8)));
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

    int32_t CopyUtf8ToBuffer(const std::string& value, char* buffer, int32_t bufferSize)
    {
        const auto required = static_cast<int32_t>(value.size() + 1);
        if (buffer == nullptr || bufferSize <= 0)
        {
            return required;
        }

        const auto copySize = (std::min)(static_cast<size_t>(bufferSize - 1), value.size());
        if (copySize > 0)
        {
            std::memcpy(buffer, value.data(), copySize);
        }
        buffer[copySize] = '\0';
        return required;
    }
}

EffekseerRendererHandle effekseer_renderer_create()
{
    return new (std::nothrow) EffectsManager();
}

void effekseer_renderer_destroy(EffekseerRendererHandle handle)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr)
    {
        return;
    }

    manager->Shutdown();
    delete manager;
}

int32_t effekseer_renderer_initialize(
    EffekseerRendererHandle handle,
    void* device,
    void* context,
    int32_t width,
    int32_t height)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr ||
        !manager->Initialize(
            static_cast<ID3D11Device*>(device),
            static_cast<ID3D11DeviceContext*>(context)))
    {
        return 0;
    }

    manager->SetProjectionPerspective(90.0f, width, height, 1.0f, 2000.0f);
    return 1;
}

int32_t effekseer_renderer_load_effect(
    EffekseerRendererHandle handle,
    const char* pathUtf8)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr)
    {
        return 0;
    }

    try
    {
        const auto path = ToPath(pathUtf8);
        return !path.empty() && manager->LoadEffect(path) ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

int32_t effekseer_renderer_get_last_error(
    EffekseerRendererHandle handle,
    char* buffer,
    int32_t bufferSize)
{
    auto* manager = GetManager(handle);
    return CopyUtf8ToBuffer(
        manager == nullptr ? std::string{} : ToUtf8(manager->GetLastErrorMessage()),
        buffer,
        bufferSize);
}

void effekseer_renderer_render(
    EffekseerRendererHandle handle,
    void* renderTarget,
    void* depthStencil,
    int32_t width,
    int32_t height)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Draw(
            static_cast<ID3D11RenderTargetView*>(renderTarget),
            static_cast<ID3D11DepthStencilView*>(depthStencil),
            width,
            height);
    }
}

void effekseer_renderer_update(EffekseerRendererHandle handle, float deltaFrames)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Update(deltaFrames / 60.0f);
    }
}

void effekseer_renderer_set_projection_perspective(
    EffekseerRendererHandle handle,
    float fov,
    int32_t width,
    int32_t height,
    float nearValue,
    float farValue)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetProjectionPerspective(fov, width, height, nearValue, farValue);
    }
}

void effekseer_renderer_set_projection_orthographic(
    EffekseerRendererHandle handle,
    float width,
    float height,
    float nearValue,
    float farValue)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetProjectionOrthographic(width, height, nearValue, farValue);
    }
}

void effekseer_renderer_set_camera_look_at(
    EffekseerRendererHandle handle,
    float positionX,
    float positionY,
    float positionZ,
    float targetX,
    float targetY,
    float targetZ,
    float upX,
    float upY,
    float upZ)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetCameraLookAt(
            positionX,
            positionY,
            positionZ,
            targetX,
            targetY,
            targetZ,
            upX,
            upY,
            upZ);
    }
}

void effekseer_renderer_set_location(
    EffekseerRendererHandle handle,
    float x,
    float y,
    float z)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetLocation(x, y, z);
    }
}

void effekseer_renderer_set_rotation(
    EffekseerRendererHandle handle,
    float x,
    float y,
    float z)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetRotation(x, y, z);
    }
}

void effekseer_renderer_set_scale(EffekseerRendererHandle handle, float scale)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetScale(scale);
    }
}

void effekseer_renderer_reset(EffekseerRendererHandle handle)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Restart();
    }
}

int32_t effekseer_renderer_get_total_frame(EffekseerRendererHandle handle)
{
    auto* manager = GetManager(handle);
    return manager == nullptr ? 0 : manager->GetTotalFrame();
}
