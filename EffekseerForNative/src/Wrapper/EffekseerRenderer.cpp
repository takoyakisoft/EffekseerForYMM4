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

    std::filesystem::path ToPath(const char* path_utf8)
    {
        if (path_utf8 == nullptr || *path_utf8 == '\0')
        {
            return {};
        }

        return std::filesystem::path(
            std::u8string(reinterpret_cast<const char8_t*>(path_utf8)));
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

    int32_t CopyUtf8ToBuffer(const std::string& value, char* buffer, int32_t buffer_size)
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
}

EffekseerRendererHandle effekseer_renderer_create()
{
    try
    {
        return new (std::nothrow) EffectsManager();
    }
    catch (...)
    {
        return nullptr;
    }
}

void effekseer_renderer_destroy(EffekseerRendererHandle handle)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr)
    {
        return;
    }

    try
    {
        manager->Shutdown();
    }
    catch (...)
    {
    }
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
    if (manager == nullptr)
    {
        return 0;
    }

    try
    {
        if (!manager->Initialize(
                static_cast<ID3D11Device*>(device),
                static_cast<ID3D11DeviceContext*>(context)))
        {
            return 0;
        }

        manager->SetProjection(width, height);
        manager->SetCamera(20.0f);
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

int32_t effekseer_renderer_load_effect(EffekseerRendererHandle handle, const char* path_utf8)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr)
    {
        return 0;
    }

    try
    {
        const auto path = ToPath(path_utf8);
        if (path.empty() || !manager->LoadEffect(path.native(), path))
        {
            return 0;
        }

        manager->PlayEffect(path.native(), 0, 0, 0);
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

int32_t effekseer_renderer_get_last_error(
    EffekseerRendererHandle handle,
    char* buffer,
    int32_t buffer_size)
{
    auto* manager = GetManager(handle);
    return CopyUtf8ToBuffer(manager == nullptr ? std::string{} : ToUtf8(manager->GetLastErrorMessage()), buffer, buffer_size);
}

void effekseer_renderer_render(EffekseerRendererHandle handle)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Draw();
    }
}

void effekseer_renderer_update(EffekseerRendererHandle handle, float delta_frames)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Update(delta_frames / 60.0f);
    }
}

void effekseer_renderer_set_sound_callback(
    EffekseerRendererHandle handle,
    void* load_sound,
    void* unload_sound,
    void* play_sound)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetSoundCallback(
            reinterpret_cast<EffekseerForNative::LoadSoundFunc>(load_sound),
            reinterpret_cast<EffekseerForNative::UnloadSoundFunc>(unload_sound),
            reinterpret_cast<EffekseerForNative::PlaySoundFunc>(play_sound));
    }
}

void effekseer_renderer_set_projection(EffekseerRendererHandle handle, int32_t width, int32_t height)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetProjection(width, height);
    }
}

void effekseer_renderer_set_projection_perspective(
    EffekseerRendererHandle handle,
    float fov,
    int32_t width,
    int32_t height,
    float near_value,
    float far_value)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetProjectionPerspective(fov, width, height, near_value, far_value);
    }
}

void effekseer_renderer_set_projection_orthographic(
    EffekseerRendererHandle handle,
    float width,
    float height,
    float near_value,
    float far_value)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetProjectionOrthographic(width, height, near_value, far_value);
    }
}

void effekseer_renderer_set_camera_look_at(
    EffekseerRendererHandle handle,
    float position_x,
    float position_y,
    float position_z,
    float target_x,
    float target_y,
    float target_z,
    float up_x,
    float up_y,
    float up_z)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetCameraLookAt(
            position_x,
            position_y,
            position_z,
            target_x,
            target_y,
            target_z,
            up_x,
            up_y,
            up_z);
    }
}

void effekseer_renderer_set_location(EffekseerRendererHandle handle, float x, float y, float z)
{
    if (auto* manager = GetManager(handle))
    {
        manager->SetLocation(x, y, z);
    }
}

void effekseer_renderer_set_rotation(EffekseerRendererHandle handle, float x, float y, float z)
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
        manager->StopAll();
        const auto key = manager->GetLastPlayedKey();
        if (!key.empty())
        {
            manager->PlayEffect(key, 0, 0, 0);
        }
    }
}

void effekseer_renderer_stop(EffekseerRendererHandle handle)
{
    if (auto* manager = GetManager(handle))
    {
        manager->StopAll();
    }
}

void effekseer_renderer_play_effect(
    EffekseerRendererHandle handle,
    const char* path_utf8,
    float x,
    float y,
    float z)
{
    if (auto* manager = GetManager(handle))
    {
        const auto path = ToPath(path_utf8);
        if (!path.empty())
        {
            manager->PlayEffect(path.native(), x, y, z);
        }
    }
}

void effekseer_renderer_shutdown(EffekseerRendererHandle handle)
{
    if (auto* manager = GetManager(handle))
    {
        manager->Shutdown();
    }
}

int32_t effekseer_renderer_get_total_frame(EffekseerRendererHandle handle)
{
    auto* manager = GetManager(handle);
    if (manager == nullptr)
    {
        return 0;
    }

    const auto key = manager->GetLastPlayedKey();
    return key.empty() ? 0 : manager->GetTotalFrame(key);
}
