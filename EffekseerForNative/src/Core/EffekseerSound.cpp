#include "EffekseerSound.h"

namespace EffekseerForNative
{
    using namespace Effekseer;

    CustomSoundLoader::CustomSoundLoader(LoadSoundFunc loadFunc, UnloadSoundFunc unloadFunc)
        : loadFunc_(loadFunc), unloadFunc_(unloadFunc)
    {
    }

    CustomSoundLoader::~CustomSoundLoader()
    {
    }

    SoundDataRef CustomSoundLoader::Load(const char16_t* path)
    {
        if (loadFunc_)
        {
            int32_t id = loadFunc_(path);
            if (id >= 0)
            {
                return MakeRefPtr<CustomSoundData>(id);
            }
        }
        return nullptr;
    }

    SoundDataRef CustomSoundLoader::Load(const void* data, int32_t size)
    {
        // Not implemented for binary blob loading via memory
        return nullptr;
    }

    void CustomSoundLoader::Unload(SoundDataRef data)
    {
        if (data != nullptr)
        {
            auto customData = (CustomSoundData*)data.Get();
            if (unloadFunc_)
            {
                unloadFunc_(customData->SoundId);
            }
        }
        data.Reset();
    }

    CustomSoundPlayer::CustomSoundPlayer(PlaySoundFunc playFunc)
        : playFunc_(playFunc)
    {
    }

    CustomSoundPlayer::~CustomSoundPlayer()
    {
    }

    SoundHandle CustomSoundPlayer::Play(SoundTag tag, const InstanceParameter& parameter)
    {
        if (parameter.Data != nullptr && playFunc_)
        {
            auto customData = (CustomSoundData*)parameter.Data.Get();
            playFunc_(customData->SoundId, parameter.Volume, parameter.Pan, parameter.Pitch, parameter.Mode3D,
                parameter.Position.X, parameter.Position.Y, parameter.Position.Z, parameter.Distance);
        }
        return nullptr;
    }

    void CustomSoundPlayer::Stop(SoundHandle handle, SoundTag tag)
    {
    }

    void CustomSoundPlayer::Pause(SoundHandle handle, SoundTag tag, bool pause)
    {
    }

    bool CustomSoundPlayer::CheckPlaying(SoundHandle handle, SoundTag tag)
    {
        return false;
    }

    void CustomSoundPlayer::StopTag(SoundTag tag)
    {
    }

    void CustomSoundPlayer::PauseTag(SoundTag tag, bool pause)
    {
    }

    bool CustomSoundPlayer::CheckPlayingTag(SoundTag tag)
    {
        return false;
    }

    void CustomSoundPlayer::StopAll()
    {
    }
}
