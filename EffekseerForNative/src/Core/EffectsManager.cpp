#include "EffectsManager.h"

#include <array>
#include <functional>
#include <string>
#include <utility>
#include <Windows.h>

namespace
{
    thread_local std::string lastEffekseerErrorUtf8;

    std::u16string ToUtf16PathString(const std::filesystem::path& path)
    {
        static_assert(
            sizeof(std::filesystem::path::value_type) == sizeof(char16_t),
            "Windows paths must use UTF-16.");
        const auto& native = path.native();
        return {native.begin(), native.end()};
    }

    std::wstring Utf8ToWide(const std::string& value)
    {
        if (value.empty())
        {
            return {};
        }

        const auto length = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            nullptr,
            0);
        if (length <= 0)
        {
            return {};
        }

        std::wstring result(static_cast<size_t>(length), L'\0');
        MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            result.data(),
            length);
        return result;
    }
}

bool EffectsManager::Initialize(ID3D11Device* device, ID3D11DeviceContext* context)
{
    lastErrorMessage_.clear();
    if ((device == nullptr) != (context == nullptr))
    {
        lastErrorMessage_ = L"Direct3D 11 device and immediate context must either both be set or both be null.";
        return false;
    }

    Effekseer::SetLogger([](Effekseer::LogType logType, const std::string& message)
        {
            if (logType == Effekseer::LogType::Error || logType == Effekseer::LogType::Warning)
            {
                lastEffekseerErrorUtf8 = message;
            }
        });

    if (device != nullptr)
    {
        renderer_ = ::EffekseerRendererDX11::Renderer::Create(
            device,
            context,
            2000,
            D3D11_COMPARISON_LESS_EQUAL,
            false);
        if (renderer_.Get() == nullptr)
        {
            lastErrorMessage_ = L"Failed to create the Effekseer Direct3D 11 renderer.";
            return false;
        }
    }

    manager_ = ::Effekseer::Manager::Create(2000);
    if (manager_.Get() == nullptr)
    {
        lastErrorMessage_ = L"Failed to create the Effekseer manager.";
        renderer_.Reset();
        return false;
    }

    if (renderer_ != nullptr)
    {
        manager_->SetSpriteRenderer(renderer_->CreateSpriteRenderer());
        manager_->SetRibbonRenderer(renderer_->CreateRibbonRenderer());
        manager_->SetRingRenderer(renderer_->CreateRingRenderer());
        manager_->SetTrackRenderer(renderer_->CreateTrackRenderer());
        manager_->SetModelRenderer(renderer_->CreateModelRenderer());
        manager_->SetTextureLoader(renderer_->CreateTextureLoader());
        manager_->SetModelLoader(renderer_->CreateModelLoader());
        manager_->SetMaterialLoader(renderer_->CreateMaterialLoader());
    }
    manager_->SetCoordinateSystem(::Effekseer::CoordinateSystem::RH);

    SetProjectionPerspective(90.0f, 1920, 1080, 1.0f, 2000.0f);
    SetCameraLookAt(0.0f, 0.0f, 20.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f);
    return true;
}

void EffectsManager::SetSoundCallbacks(
    EffekseerForNative::LoadSoundFunc loadSound,
    EffekseerForNative::UnloadSoundFunc unloadSound,
    EffekseerForNative::PlaySoundFunc playSound)
{
    if (manager_ == nullptr)
    {
        return;
    }

    auto setting = manager_->GetSetting();
    if (setting != nullptr)
    {
        setting->SetSoundLoader(
            ::Effekseer::MakeRefPtr<EffekseerForNative::CustomSoundLoader>(
                loadSound,
                unloadSound));
    }
    manager_->SetSoundPlayer(
        ::Effekseer::MakeRefPtr<EffekseerForNative::CustomSoundPlayer>(playSound));
}

void EffectsManager::Shutdown()
{
    effect_.Reset();
    manager_.Reset();
    renderer_.Reset();
    activeHandle_ = -1;
    totalFrame_ = 0;
}

bool EffectsManager::LoadEffect(const std::filesystem::path& path)
{
    if (manager_.Get() == nullptr)
    {
        return false;
    }

    lastErrorMessage_.clear();
    lastEffekseerErrorUtf8.clear();

    const auto effectPath = ToUtf16PathString(path);
    auto directoryPath = ToUtf16PathString(path.parent_path());
    if (!directoryPath.empty() &&
        directoryPath.back() != u'\\' &&
        directoryPath.back() != u'/')
    {
        directoryPath.push_back(u'\\');
    }
    auto effect = ::Effekseer::Effect::Create(
        manager_->GetSetting(),
        effectPath.c_str(),
        1.0f,
        directoryPath.empty() ? nullptr : directoryPath.c_str());
    if (effect == nullptr)
    {
        lastErrorMessage_ = Utf8ToWide(lastEffekseerErrorUtf8);
        if (lastErrorMessage_.empty())
        {
            lastErrorMessage_ = L"Failed to load the Effekseer effect file.";
        }
        return false;
    }

    manager_->StopAllEffects();
    effect_ = effect;
    totalFrame_ = effect_->CalculateTerm().TermMax;
    randomSeed_ = static_cast<int32_t>(
        std::hash<std::filesystem::path::string_type>{}(path.native()) & 0x7fffffff);
    if (renderer_ != nullptr)
    {
        renderer_->SetTime(0.0f);
    }
    PlayLoadedEffect();
    return true;
}

void EffectsManager::Restart()
{
    if (manager_.Get() == nullptr || effect_ == nullptr)
    {
        return;
    }

    manager_->StopAllEffects();
    if (renderer_ != nullptr)
    {
        renderer_->SetTime(0.0f);
    }
    PlayLoadedEffect();
}

void EffectsManager::PlayLoadedEffect()
{
    activeHandle_ = manager_->Play(effect_, 0.0f, 0.0f, 0.0f);
    manager_->SetRandomSeed(activeHandle_, randomSeed_);
    manager_->SetScale(activeHandle_, scale_, scale_, scale_);
    manager_->SetLocation(activeHandle_, locationX_, locationY_, locationZ_);
    manager_->SetRotation(activeHandle_, rotationX_, rotationY_, rotationZ_);
}

bool EffectsManager::HasActiveEffect() const
{
    return manager_.Get() != nullptr &&
        activeHandle_ >= 0 &&
        manager_->Exists(activeHandle_);
}

void EffectsManager::Update(float deltaSeconds)
{
    if (manager_.Get() == nullptr)
    {
        return;
    }

    manager_->Update(deltaSeconds * 60.0f);
    if (renderer_ != nullptr)
    {
        renderer_->SetTime(renderer_->GetTime() + deltaSeconds);
    }
}

void EffectsManager::Draw(
    ID3D11RenderTargetView* renderTarget,
    ID3D11DepthStencilView* depthStencil,
    int width,
    int height)
{
    if (manager_.Get() == nullptr ||
        renderer_.Get() == nullptr ||
        renderTarget == nullptr ||
        depthStencil == nullptr)
    {
        return;
    }

    auto* context = renderer_->GetContext();

    std::array<ID3D11RenderTargetView*, D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT> previousRenderTargets{};
    ID3D11DepthStencilView* previousDepthStencil = nullptr;
    context->OMGetRenderTargets(
        static_cast<UINT>(previousRenderTargets.size()),
        previousRenderTargets.data(),
        &previousDepthStencil);

    std::array<D3D11_VIEWPORT, D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE> previousViewports{};
    UINT previousViewportCount = static_cast<UINT>(previousViewports.size());
    context->RSGetViewports(&previousViewportCount, previousViewports.data());

    constexpr float clearColor[4] = {};
    context->ClearRenderTargetView(renderTarget, clearColor);
    context->ClearDepthStencilView(
        depthStencil,
        D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL,
        1.0f,
        0);
    context->OMSetRenderTargets(1, &renderTarget, depthStencil);

    const D3D11_VIEWPORT viewport{
        0.0f,
        0.0f,
        static_cast<float>(width),
        static_cast<float>(height),
        0.0f,
        1.0f,
    };
    context->RSSetViewports(1, &viewport);

    renderer_->SetProjectionMatrix(projection_);
    renderer_->SetCameraMatrix(camera_);
    renderer_->BeginRendering();
    manager_->Draw();
    renderer_->EndRendering();

    context->RSSetViewports(
        previousViewportCount,
        previousViewportCount == 0 ? nullptr : previousViewports.data());
    context->OMSetRenderTargets(
        static_cast<UINT>(previousRenderTargets.size()),
        previousRenderTargets.data(),
        previousDepthStencil);

    for (auto* previousRenderTarget : previousRenderTargets)
    {
        if (previousRenderTarget != nullptr)
        {
            previousRenderTarget->Release();
        }
    }
    if (previousDepthStencil != nullptr)
    {
        previousDepthStencil->Release();
    }
}

void EffectsManager::SetProjectionPerspective(
    float fov,
    int width,
    int height,
    float nearValue,
    float farValue)
{
    projection_.PerspectiveFovRH(
        fov / 180.0f * 3.14159265358979323846f,
        static_cast<float>(width) / static_cast<float>(height),
        nearValue,
        farValue);
}

void EffectsManager::SetProjectionOrthographic(
    float width,
    float height,
    float nearValue,
    float farValue)
{
    projection_.OrthographicRH(width, height, nearValue, farValue);
}

void EffectsManager::SetCameraLookAt(
    float positionX,
    float positionY,
    float positionZ,
    float targetX,
    float targetY,
    float targetZ,
    float upX,
    float upY,
    float upZ)
{
    camera_.LookAtRH(
        ::Effekseer::Vector3D(positionX, positionY, positionZ),
        ::Effekseer::Vector3D(targetX, targetY, targetZ),
        ::Effekseer::Vector3D(upX, upY, upZ));
}

void EffectsManager::SetLocation(float x, float y, float z)
{
    locationX_ = x;
    locationY_ = y;
    locationZ_ = z;
    if (HasActiveEffect())
    {
        manager_->SetLocation(activeHandle_, x, y, z);
    }
}

void EffectsManager::SetRotation(float x, float y, float z)
{
    rotationX_ = x;
    rotationY_ = y;
    rotationZ_ = z;
    if (HasActiveEffect())
    {
        manager_->SetRotation(activeHandle_, x, y, z);
    }
}

void EffectsManager::SetScale(float scale)
{
    scale_ = scale;
    if (HasActiveEffect())
    {
        manager_->SetScale(activeHandle_, scale, scale, scale);
    }
}

int EffectsManager::GetTotalFrame() const
{
    return totalFrame_;
}

const std::wstring& EffectsManager::GetLastErrorMessage() const
{
    return lastErrorMessage_;
}

void EffectsManager::SetLastErrorMessage(std::wstring message)
{
    lastErrorMessage_ = std::move(message);
}
