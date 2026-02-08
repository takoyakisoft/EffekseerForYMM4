#include "EffectsManager.h"

#include <filesystem>
#include <chrono>

bool EffectsManager::Initialize(ID3D11Device* device, ID3D11DeviceContext* context)
{
    if (device != nullptr && context != nullptr)
    {
        renderer_ = ::EffekseerRendererDX11::Renderer::Create(device, context, 2000, D3D11_COMPARISON_LESS_EQUAL, false);
        if (renderer_.Get() == nullptr) return false;
    }

    manager_ = ::Effekseer::Manager::Create(2000);
    if (manager_.Get() == nullptr) return false;

    if (renderer_.Get() != nullptr)
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
    else
    {
        // Headless mode: dummy loaders
        // We need minimal loaders to avoid crashes during effect loading
        class DummyTextureLoader : public Effekseer::TextureLoader {
        public:
            Effekseer::TextureRef Load(const char16_t* path, Effekseer::TextureType textureType) override { return nullptr; }
            Effekseer::TextureRef Load(const void* data, int32_t size, Effekseer::TextureType textureType, bool isMipMapEnabled) override { return nullptr; }
            void Unload(Effekseer::TextureRef data) override {}
        };
        class DummyModelLoader : public Effekseer::ModelLoader {
        public:
            Effekseer::ModelRef Load(const char16_t* path) override { return nullptr; }
            Effekseer::ModelRef Load(const void* data, int32_t size) override { return nullptr; }
            void Unload(Effekseer::ModelRef data) override {}
        };
        class DummyMaterialLoader : public Effekseer::MaterialLoader {
        public:
            Effekseer::MaterialRef Load(const char16_t* path) override { return nullptr; }
            Effekseer::MaterialRef Load(const void* data, int32_t size, Effekseer::MaterialFileType fileType) override { return nullptr; }
            void Unload(Effekseer::MaterialRef data) override {}
        };
        class DummyCurveLoader : public Effekseer::CurveLoader {
        public:
            Effekseer::CurveRef Load(const char16_t* path) override { return nullptr; }
            Effekseer::CurveRef Load(const void* data, int32_t size) override { return nullptr; }
            void Unload(Effekseer::CurveRef data) override {}
        };

        manager_->SetTextureLoader(Effekseer::MakeRefPtr<DummyTextureLoader>());
        manager_->SetModelLoader(Effekseer::MakeRefPtr<DummyModelLoader>());
        manager_->SetMaterialLoader(Effekseer::MakeRefPtr<DummyMaterialLoader>());
        manager_->SetCurveLoader(Effekseer::MakeRefPtr<DummyCurveLoader>());
    }
   
    manager_->SetCoordinateSystem(::Effekseer::CoordinateSystem::RH);

    if (renderer_.Get() != nullptr)
    {
        SetCamera(cameraDistance_);
    }
    return true; 
}

void EffectsManager::SetSoundCallback(EffekseerForNative::LoadSoundFunc loadSound, EffekseerForNative::UnloadSoundFunc unloadSound, EffekseerForNative::PlaySoundFunc playSound)
{
    if (manager_ != nullptr)
    {
        auto setting = manager_->GetSetting();
        if (setting != nullptr)
        {
            auto loader = Effekseer::MakeRefPtr<EffekseerForNative::CustomSoundLoader>(loadSound, unloadSound);
            setting->SetSoundLoader(loader);
        }

        auto player = Effekseer::MakeRefPtr<EffekseerForNative::CustomSoundPlayer>(playSound);
        manager_->SetSoundPlayer(player);
    }
}

void EffectsManager::Shutdown()
{
    effects_.clear();
    manager_.Reset();
    renderer_.Reset();
}

void EffectsManager::Update(float deltaSeconds)
{
    if (manager_.Get() == nullptr) return;
    float deltaFrames = deltaSeconds * 60.0f;
    manager_->Update(deltaFrames);
    if (renderer_.Get() != nullptr)
    {
        renderer_->SetTime(renderer_->GetTime() + deltaSeconds);
    }

    // Stop effects by duration or term
    for (size_t i = 0; i < active_.size();)
    {
        auto& a = active_[i];
        if (!manager_->Exists(a.handle))
        {
            active_.erase(active_.begin() + i);
            continue;
        }

        a.elapsedTime += deltaSeconds;
        double elapsed = a.elapsedTime;

        if (maxDurationSeconds_ > 0 && elapsed >= (double)maxDurationSeconds_)
        {
            manager_->StopEffect(a.handle);
            active_.erase(active_.begin() + i);
            continue;
        }

        if (a.termMax > 0 && a.termMax < INT_MAX)
        {
            double elapsedFrames = elapsed * 60.0 * speed_;
            if (elapsedFrames >= a.termMax)
            {
                manager_->StopEffect(a.handle);
                active_.erase(active_.begin() + i);
                continue;
            }
        }
        ++i;
    }
}

void EffectsManager::Draw()
{
    if (manager_.Get() == nullptr) return;
    if (renderer_.Get() == nullptr) return;

    renderer_->SetProjectionMatrix(projection_);
    renderer_->SetCameraMatrix(camera_);
    renderer_->BeginRendering();
    manager_->Draw();
    renderer_->EndRendering();
}

bool EffectsManager::LoadEffect(const std::wstring& key, const std::wstring& path)
{
    if (manager_.Get() == nullptr) return false;
    std::filesystem::path p(path);
    std::wstring dir = p.parent_path().wstring();
    if (!dir.empty() && dir.back() != L'\\') dir += L'\\';

    auto effect = ::Effekseer::Effect::Create(
        manager_->GetSetting(),
        (const char16_t*)path.c_str(),
        1.0f,
        (const char16_t*)dir.c_str());
    if (effect == nullptr) return false;
    effects_[key] = effect;

    return true;
}

void EffectsManager::PlayEffect(const std::wstring& key, float x, float y, float z)
{
    if (manager_.Get() == nullptr) return;
    auto it = effects_.find(key);
    if (it == effects_.end()) return;
    auto handle = manager_->Play(it->second, x, y, z);
    lastPlayedKey_ = key;
    manager_->SetSpeed(handle, speed_);

    int32_t termMax = 0;
    if (it->second != nullptr)
    {
        auto term = it->second->CalculateTerm();
        termMax = term.TermMax;
    }

    ActiveEffect a;
    a.handle = handle;
    a.elapsedTime = 0.0;
    a.termMax = termMax;
    a.key = key;
    active_.push_back(a);
    
    // Apply current settings
    manager_->SetSpeed(handle, speed_);
    manager_->SetScale(handle, scale_, scale_, scale_);
}


void EffectsManager::StopAll()
{
    if (manager_.Get()) manager_->StopAllEffects();
    active_.clear();
}

void EffectsManager::SetProjection(int width, int height)
{
    SetProjectionPerspective(90.0f, width, height, 1.0f, 2000.0f);
}

void EffectsManager::SetProjectionPerspective(float fov, int width, int height, float nearVal, float farVal)
{
    screenWidth_ = width;
    screenHeight_ = height;
    projection_.PerspectiveFovRH(fov / 180.0f * 3.14159f, (float)width / (float)height, nearVal, farVal);
}

void EffectsManager::SetProjectionOrthographic(float width, float height, float nearVal, float farVal)
{
    screenWidth_ = (int)width;
    screenHeight_ = (int)height;
    projection_.OrthographicRH(width, height, nearVal, farVal);
}

void EffectsManager::SetCamera(float distance)
{
    cameraDistance_ = distance;
    ::Effekseer::Vector3D pos(0.0f, 0.0f, cameraDistance_);
    ::Effekseer::Vector3D target(0.0f, 0.0f, 0.0f);
    ::Effekseer::Vector3D up(0.0f, 1.0f, 0.0f);
    camera_.LookAtRH(pos, target, up);
}

void EffectsManager::SetCameraLookAt(float posX, float posY, float posZ, float targetX, float targetY, float targetZ, float upX, float upY, float upZ)
{
    ::Effekseer::Vector3D pos(posX, posY, posZ);
    ::Effekseer::Vector3D target(targetX, targetY, targetZ);
    ::Effekseer::Vector3D up(upX, upY, upZ);
    camera_.LookAtRH(pos, target, up);
}

void EffectsManager::SetSpeed(float speed)
{
    speed_ = speed;
    if (manager_.Get() == nullptr) return;
    for (auto& a : active_)
    {
        manager_->SetSpeed(a.handle, speed_);
    }
}

void EffectsManager::SetScale(float scale)
{
    scale_ = scale;
    if (manager_.Get() == nullptr) return;
    for (auto& a : active_)
    {
        manager_->SetScale(a.handle, scale_, scale_, scale_);
    }
}


void EffectsManager::SetMaxDurationSeconds(int seconds)
{
    maxDurationSeconds_ = seconds;
}

const std::wstring& EffectsManager::GetLastPlayedKey() const
{
    return lastPlayedKey_;
}


int EffectsManager::GetTotalFrame(const std::wstring& key) const
{
    auto it = effects_.find(key);
    if (it == effects_.end() || it->second == nullptr) return 0;
    
    auto term = it->second->CalculateTerm();
    return term.TermMax;
}
