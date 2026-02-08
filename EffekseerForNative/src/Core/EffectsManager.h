#pragma once

#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <Effekseer.h>
#include <EffekseerRendererDX11.h>
#include "EffekseerSound.h"


class EffectsManager
{
public:
    bool Initialize(ID3D11Device* device, ID3D11DeviceContext* context);
    void Shutdown();

    void SetSoundCallback(EffekseerForNative::LoadSoundFunc loadSound, EffekseerForNative::UnloadSoundFunc unloadSound, EffekseerForNative::PlaySoundFunc playSound);


    void Update(float deltaSeconds);
    void Draw();

    bool LoadEffect(const std::wstring& key, const std::wstring& path);
    void PlayEffect(const std::wstring& key, float x, float y, float z = 0.0f);

    void StopAll();

    void SetProjection(int width, int height);
    void SetProjectionPerspective(float fov, int width, int height, float nearVal, float farVal);
    void SetProjectionOrthographic(float width, float height, float nearVal, float farVal);
    void SetCamera(float distance);
    void SetCameraLookAt(float posX, float posY, float posZ, float targetX, float targetY, float targetZ, float upX, float upY, float upZ);
    void SetSpeed(float speed);
    void SetScale(float scale);
    void SetMaxDurationSeconds(int seconds);
    const std::wstring& GetLastPlayedKey() const;
    int GetTotalFrame(const std::wstring& key) const;

private:
    struct ActiveEffect
    {
        ::Effekseer::Handle handle = -1;
        double elapsedTime = 0.0;
        int32_t termMax = 0;
        std::wstring key;
    };


    ::Effekseer::ManagerRef manager_;
    ::EffekseerRendererDX11::RendererRef renderer_;

    std::unordered_map<std::wstring, ::Effekseer::EffectRef> effects_;
    std::wstring lastPlayedKey_;

    ::Effekseer::Matrix44 projection_;
    ::Effekseer::Matrix44 camera_;
    float cameraDistance_ = 50.0f;
    int screenWidth_ = 1920;
    int screenHeight_ = 1080;
    float speed_ = 1.0f;
    float scale_ = 1.0f;
    int maxDurationSeconds_ = 0;
    std::vector<ActiveEffect> active_;
};
