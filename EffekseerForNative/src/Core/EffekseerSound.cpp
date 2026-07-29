#include "EffekseerSound.h"
#include "WindowsString.h"

#include <string>

namespace EffekseerForNative {
CustomSoundLoader::CustomSoundLoader(LoadSoundFunc loadSound,
                                     UnloadSoundFunc unloadSound)
    : loadSound_(loadSound), unloadSound_(unloadSound) {}

::Effekseer::SoundDataRef CustomSoundLoader::Load(const char16_t *path) {
  if (loadSound_ == nullptr || path == nullptr) {
    return nullptr;
  }

  static_assert(sizeof(wchar_t) == sizeof(char16_t));
  const auto pathUtf8 = EffekseerForYMM4::WindowsString::WideToUtf8(
      std::wstring(reinterpret_cast<const wchar_t *>(path)));
  const auto id = loadSound_(pathUtf8.c_str());
  return id < 0 ? nullptr : ::Effekseer::MakeRefPtr<CustomSoundData>(id);
}

::Effekseer::SoundDataRef CustomSoundLoader::Load(const void *, int32_t) {
  return nullptr;
}

void CustomSoundLoader::Unload(::Effekseer::SoundDataRef data) {
  if (data != nullptr && unloadSound_ != nullptr) {
    const auto *sound = static_cast<CustomSoundData *>(data.Get());
    unloadSound_(sound->SoundId);
  }
  data.Reset();
}

CustomSoundPlayer::CustomSoundPlayer(PlaySoundFunc playSound)
    : playSound_(playSound) {}

::Effekseer::SoundHandle
CustomSoundPlayer::Play(::Effekseer::SoundTag,
                        const InstanceParameter &parameter) {
  if (parameter.Data != nullptr && playSound_ != nullptr) {
    const auto *sound = static_cast<CustomSoundData *>(parameter.Data.Get());
    playSound_(sound->SoundId, parameter.Volume, parameter.Pan, parameter.Pitch,
               parameter.Mode3D, parameter.Position.X, parameter.Position.Y,
               parameter.Position.Z, parameter.Distance);
  }
  return nullptr;
}

void CustomSoundPlayer::Stop(::Effekseer::SoundHandle, ::Effekseer::SoundTag) {}

void CustomSoundPlayer::Pause(::Effekseer::SoundHandle, ::Effekseer::SoundTag,
                              bool) {}

bool CustomSoundPlayer::CheckPlaying(::Effekseer::SoundHandle,
                                     ::Effekseer::SoundTag) {
  return false;
}

void CustomSoundPlayer::StopTag(::Effekseer::SoundTag) {}

void CustomSoundPlayer::PauseTag(::Effekseer::SoundTag, bool) {}

bool CustomSoundPlayer::CheckPlayingTag(::Effekseer::SoundTag) { return false; }

void CustomSoundPlayer::StopAll() {}
} // namespace EffekseerForNative
