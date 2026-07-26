#pragma once

#include "../../vendor/effekseer/src/Effekseer/Effekseer/Effekseer.SoundLoader.h"
#include "../../vendor/effekseer/src/Effekseer/Effekseer/Sound/Effekseer.SoundPlayer.h"

#include <cstdint>

namespace EffekseerForNative {
using LoadSoundFunc = int32_t(EFK_STDCALL *)(const char *pathUtf8);
using UnloadSoundFunc = void(EFK_STDCALL *)(int32_t id);
using PlaySoundFunc = void(EFK_STDCALL *)(int32_t id, float volume, float pan,
                                          float pitch, bool mode3d, float x,
                                          float y, float z, float distance);

class CustomSoundData final : public ::Effekseer::SoundData {
public:
  explicit CustomSoundData(int32_t id) : SoundId(id) {}

  int32_t SoundId;
};

class CustomSoundLoader final : public ::Effekseer::SoundLoader {
public:
  CustomSoundLoader(LoadSoundFunc loadSound, UnloadSoundFunc unloadSound);

  ::Effekseer::SoundDataRef Load(const char16_t *path) override;
  ::Effekseer::SoundDataRef Load(const void *data, int32_t size) override;
  void Unload(::Effekseer::SoundDataRef data) override;

private:
  LoadSoundFunc loadSound_;
  UnloadSoundFunc unloadSound_;
};

class CustomSoundPlayer final : public ::Effekseer::SoundPlayer {
public:
  explicit CustomSoundPlayer(PlaySoundFunc playSound);

  ::Effekseer::SoundHandle Play(::Effekseer::SoundTag tag,
                                const InstanceParameter &parameter) override;
  void Stop(::Effekseer::SoundHandle handle,
            ::Effekseer::SoundTag tag) override;
  void Pause(::Effekseer::SoundHandle handle, ::Effekseer::SoundTag tag,
             bool pause) override;
  bool CheckPlaying(::Effekseer::SoundHandle handle,
                    ::Effekseer::SoundTag tag) override;
  void StopTag(::Effekseer::SoundTag tag) override;
  void PauseTag(::Effekseer::SoundTag tag, bool pause) override;
  bool CheckPlayingTag(::Effekseer::SoundTag tag) override;
  void StopAll() override;

private:
  PlaySoundFunc playSound_;
};
} // namespace EffekseerForNative
