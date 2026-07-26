#pragma once

#include <cstdint>

#if defined(_WIN32)
#define EFFEKSEER_NATIVE_API extern "C" __declspec(dllexport)
#else
#define EFFEKSEER_NATIVE_API extern "C"
#endif

using EffekseerRendererHandle = void*;

EFFEKSEER_NATIVE_API EffekseerRendererHandle effekseer_renderer_create();
EFFEKSEER_NATIVE_API void effekseer_renderer_destroy(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_initialize(
    EffekseerRendererHandle handle,
    void* device,
    void* context,
    int32_t width,
    int32_t height);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_load_effect(
    EffekseerRendererHandle handle,
    const char* path_utf8);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_get_last_error(
    EffekseerRendererHandle handle,
    char* buffer,
    int32_t buffer_size);
EFFEKSEER_NATIVE_API void effekseer_renderer_render(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API void effekseer_renderer_update(EffekseerRendererHandle handle, float delta_frames);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_sound_callback(
    EffekseerRendererHandle handle,
    void* load_sound,
    void* unload_sound,
    void* play_sound);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_projection(
    EffekseerRendererHandle handle,
    int32_t width,
    int32_t height);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_projection_perspective(
    EffekseerRendererHandle handle,
    float fov,
    int32_t width,
    int32_t height,
    float near_value,
    float far_value);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_projection_orthographic(
    EffekseerRendererHandle handle,
    float width,
    float height,
    float near_value,
    float far_value);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_camera_look_at(
    EffekseerRendererHandle handle,
    float position_x,
    float position_y,
    float position_z,
    float target_x,
    float target_y,
    float target_z,
    float up_x,
    float up_y,
    float up_z);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_location(
    EffekseerRendererHandle handle,
    float x,
    float y,
    float z);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_rotation(
    EffekseerRendererHandle handle,
    float x,
    float y,
    float z);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_scale(EffekseerRendererHandle handle, float scale);
EFFEKSEER_NATIVE_API void effekseer_renderer_reset(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API void effekseer_renderer_stop(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API void effekseer_renderer_play_effect(
    EffekseerRendererHandle handle,
    const char* path_utf8,
    float x,
    float y,
    float z);
EFFEKSEER_NATIVE_API void effekseer_renderer_shutdown(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_get_total_frame(EffekseerRendererHandle handle);
