#pragma once

#include "EffekseerRenderer.h"

EFFEKSEER_NATIVE_API int32_t effekseer_path_combine(
    const char* base_path_utf8,
    const char* child_path_utf8,
    char* buffer,
    int32_t buffer_size);
EFFEKSEER_NATIVE_API int32_t effekseer_path_ensure_directory(const char* path_utf8);
EFFEKSEER_NATIVE_API int32_t effekseer_path_exists(const char* path_utf8);
EFFEKSEER_NATIVE_API int32_t effekseer_path_write_utf8_text(
    const char* path_utf8,
    const char* content_utf8);
EFFEKSEER_NATIVE_API int32_t effekseer_path_read_utf8_text(
    const char* path_utf8,
    char* buffer,
    int32_t buffer_size);
EFFEKSEER_NATIVE_API int32_t effekseer_path_get_last_error(char* buffer, int32_t buffer_size);
