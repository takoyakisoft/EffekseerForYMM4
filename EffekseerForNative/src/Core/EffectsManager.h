#pragma once

#include <filesystem>
#include <string>

#include <Effekseer.h>
#include <EffekseerRendererDX11.h>

class EffectsManager
{
public:
    bool Initialize(ID3D11Device* device, ID3D11DeviceContext* context);
    void Shutdown();

    bool LoadEffect(const std::filesystem::path& path);
    void Restart();
    void Update(float deltaSeconds);
    void Draw(
        ID3D11RenderTargetView* renderTarget,
        ID3D11DepthStencilView* depthStencil,
        int width,
        int height);

    void SetProjectionPerspective(float fov, int width, int height, float nearValue, float farValue);
    void SetProjectionOrthographic(float width, float height, float nearValue, float farValue);
    void SetCameraLookAt(
        float positionX,
        float positionY,
        float positionZ,
        float targetX,
        float targetY,
        float targetZ,
        float upX,
        float upY,
        float upZ);
    void SetLocation(float x, float y, float z);
    void SetRotation(float x, float y, float z);
    void SetScale(float scale);

    int GetTotalFrame() const;
    const std::wstring& GetLastErrorMessage() const;

private:
    void PlayLoadedEffect();
    bool HasActiveEffect() const;

    ::Effekseer::ManagerRef manager_;
    ::EffekseerRendererDX11::RendererRef renderer_;
    ::Effekseer::EffectRef effect_;
    ::Effekseer::Handle activeHandle_ = -1;

    ::Effekseer::Matrix44 projection_;
    ::Effekseer::Matrix44 camera_;
    float scale_ = 1.0f;
    float locationX_ = 0.0f;
    float locationY_ = 0.0f;
    float locationZ_ = 0.0f;
    float rotationX_ = 0.0f;
    float rotationY_ = 0.0f;
    float rotationZ_ = 0.0f;
    int32_t randomSeed_ = 0;
    int totalFrame_ = 0;
    std::wstring lastErrorMessage_;
};
