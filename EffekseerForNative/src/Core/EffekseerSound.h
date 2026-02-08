#pragma once

// Do not redefine Effekseer classes.
// Instead, import them from the vendor headers or rely on Effekseer.h if possible.
// Since Effekseer.h does not define SoundPlayer, we use the internal headers,
// which is acceptable given we are statically linking and need to extend the engine.
// We use relative paths to the vendor directory.

#include "../../vendor/effekseer/src/Effekseer/Effekseer/Effekseer.SoundLoader.h"
#include "../../vendor/effekseer/src/Effekseer/Effekseer/Sound/Effekseer.SoundPlayer.h"

namespace EffekseerForNative
{
    using namespace Effekseer;

    class CustomSoundData : public SoundData
    {
    public:
        int32_t SoundId = -1;
        CustomSoundData(int32_t id) : SoundId(id) {}
        virtual ~CustomSoundData() {}
    };

    typedef int32_t(EFK_STDCALL* LoadSoundFunc)(const char16_t* path);
    typedef void(EFK_STDCALL* UnloadSoundFunc)(int32_t id);
    typedef void(EFK_STDCALL* PlaySoundFunc)(int32_t id, float volume, float pan, float pitch, bool mode3d, float x, float y, float z, float distance);

    class CustomSoundLoader : public SoundLoader
    {
        LoadSoundFunc loadFunc_ = nullptr;
        UnloadSoundFunc unloadFunc_ = nullptr;

    public:
        CustomSoundLoader(LoadSoundFunc loadFunc, UnloadSoundFunc unloadFunc);
        virtual ~CustomSoundLoader();

        SoundDataRef Load(const char16_t* path) override;
        SoundDataRef Load(const void* data, int32_t size) override;
        void Unload(SoundDataRef data) override;
    };

    class CustomSoundPlayer : public SoundPlayer
    {
        PlaySoundFunc playFunc_ = nullptr;

    public:
        CustomSoundPlayer(PlaySoundFunc playFunc);
        virtual ~CustomSoundPlayer();

        SoundHandle Play(SoundTag tag, const InstanceParameter& parameter) override;
        void Stop(SoundHandle handle, SoundTag tag) override;
        void Pause(SoundHandle handle, SoundTag tag, bool pause) override;
        bool CheckPlaying(SoundHandle handle, SoundTag tag) override;
        void StopTag(SoundTag tag) override;
        void PauseTag(SoundTag tag, bool pause) override;
        bool CheckPlayingTag(SoundTag tag) override;
        void StopAll() override;
    };
}
