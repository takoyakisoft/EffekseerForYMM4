#pragma once

#include <cstdint>

#define EFFEKSEER_NATIVE_API extern "C" __declspec(dllexport)

using EffekseerRendererHandle = void *;

EFFEKSEER_NATIVE_API EffekseerRendererHandle effekseer_renderer_create();
EFFEKSEER_NATIVE_API void
effekseer_renderer_destroy(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API int32_t
effekseer_renderer_initialize(EffekseerRendererHandle handle, void *device,
                              void *context, int32_t width, int32_t height);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_load_effect(
    EffekseerRendererHandle handle, const char *pathUtf8);
EFFEKSEER_NATIVE_API int32_t effekseer_renderer_get_last_error(
    EffekseerRendererHandle handle, char *buffer, int32_t bufferSize);
EFFEKSEER_NATIVE_API void
effekseer_renderer_render(EffekseerRendererHandle handle, void *renderTarget,
                          void *depthStencil, int32_t width, int32_t height);
EFFEKSEER_NATIVE_API void
effekseer_renderer_update(EffekseerRendererHandle handle, float deltaFrames);
EFFEKSEER_NATIVE_API void
effekseer_renderer_compute(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API void
effekseer_renderer_set_sound_callbacks(EffekseerRendererHandle handle,
                                       void *loadSound, void *unloadSound,
                                       void *playSound);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_projection_perspective(
    EffekseerRendererHandle handle, float fov, int32_t width, int32_t height,
    float nearValue, float farValue);
EFFEKSEER_NATIVE_API void
effekseer_renderer_set_projection_orthographic(EffekseerRendererHandle handle,
                                               float width, float height,
                                               float nearValue, float farValue);
EFFEKSEER_NATIVE_API void effekseer_renderer_set_camera_look_at(
    EffekseerRendererHandle handle, float positionX, float positionY,
    float positionZ, float targetX, float targetY, float targetZ, float upX,
    float upY, float upZ);
EFFEKSEER_NATIVE_API void
effekseer_renderer_set_location(EffekseerRendererHandle handle, float x,
                                float y, float z);
EFFEKSEER_NATIVE_API void
effekseer_renderer_set_rotation(EffekseerRendererHandle handle, float x,
                                float y, float z);
EFFEKSEER_NATIVE_API void
effekseer_renderer_set_scale(EffekseerRendererHandle handle, float scale);
EFFEKSEER_NATIVE_API void
effekseer_renderer_reset(EffekseerRendererHandle handle);
EFFEKSEER_NATIVE_API int32_t
effekseer_renderer_get_total_frame(EffekseerRendererHandle handle);
