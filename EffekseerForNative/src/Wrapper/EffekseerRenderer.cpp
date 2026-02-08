#include "EffekseerRenderer.h"
#include "../Core/EffectsManager.h"
#include <msclr/marshal_cppstd.h>

using namespace System::Runtime::InteropServices;
using namespace System;

namespace EffekseerForNative {

    EffekseerRenderer::EffekseerRenderer()
    {
        m_impl = new EffectsManager();
    }

    EffekseerRenderer::~EffekseerRenderer()
    {
        this->!EffekseerRenderer();
    }

    EffekseerRenderer::!EffekseerRenderer()
    {
        Destroy();
        if (m_impl)
        {
            delete m_impl;
            m_impl = nullptr;
        }
    }

    bool EffekseerRenderer::Initialize(IntPtr device, IntPtr context, int width, int height)
    {
        if (!m_impl) return false;

        ID3D11Device* d3d11Device = nullptr;
        ID3D11DeviceContext* d3d11Context = nullptr;

        if (device != IntPtr::Zero)
            d3d11Device = (ID3D11Device*)device.ToPointer();

        if (context != IntPtr::Zero)
            d3d11Context = (ID3D11DeviceContext*)context.ToPointer();

        if (!m_impl->Initialize(d3d11Device, d3d11Context))
        {
            return false;
        }

        m_impl->SetProjection(width, height);
        m_impl->SetCamera(20.0f);

        return true;
    }

    bool EffekseerRenderer::LoadEffect(System::String^ path)
    {
        if (!m_impl) return false;

        std::wstring wpath = msclr::interop::marshal_as<std::wstring>(path);
        std::wstring key = wpath; // Use path as key

        if (!m_impl->LoadEffect(key, wpath))
        {
            return false;
        }

        m_impl->PlayEffect(key, 0, 0, 0);

        return true;
    }

    void EffekseerRenderer::Render()
    {
        if (m_impl)
        {
            m_impl->Draw();
        }
    }

    void EffekseerRenderer::Update(float deltaFrames)
    {
        if (m_impl)
        {
            // Convert frames to seconds (assuming 60fps base)
            m_impl->Update(deltaFrames / 60.0f);
        }
    }

    void EffekseerRenderer::SetSoundCallback(System::IntPtr loadSound, System::IntPtr unloadSound, System::IntPtr playSound)
    {
        if (m_impl)
        {
            m_impl->SetSoundCallback(
                (EffekseerForNative::LoadSoundFunc)loadSound.ToPointer(),
                (EffekseerForNative::UnloadSoundFunc)unloadSound.ToPointer(),
                (EffekseerForNative::PlaySoundFunc)playSound.ToPointer()
            );
        }
    }

    void EffekseerRenderer::SetProjection(int width, int height)
    {
        if (m_impl)
        {
            m_impl->SetProjection(width, height);
        }
    }

    void EffekseerRenderer::SetProjectionPerspective(float fov, int width, int height, float nearVal, float farVal)
    {
        if (m_impl)
        {
            m_impl->SetProjectionPerspective(fov, width, height, nearVal, farVal);
        }
    }

    void EffekseerRenderer::SetProjectionOrthographic(float width, float height, float nearVal, float farVal)
    {
        if (m_impl)
        {
            m_impl->SetProjectionOrthographic(width, height, nearVal, farVal);
        }
    }

    void EffekseerRenderer::SetCameraLookAt(float posX, float posY, float posZ, float targetX, float targetY, float targetZ, float upX, float upY, float upZ)
    {
        if (m_impl)
        {
            m_impl->SetCameraLookAt(posX, posY, posZ, targetX, targetY, targetZ, upX, upY, upZ);
        }
    }

    void EffekseerRenderer::SetScale(float scale)
    {
        if (m_impl)
        {
            m_impl->SetScale(scale);
        }
    }

    void EffekseerRenderer::Reset()
    {
        if (m_impl)
        {
            m_impl->StopAll();

            std::wstring lastKey = m_impl->GetLastPlayedKey();
            if (!lastKey.empty())
            {
                m_impl->PlayEffect(lastKey, 0, 0, 0);
            }
        }
    }

    void EffekseerRenderer::StopRoot()
    {
        if (m_impl)
        {
            m_impl->StopAll();
        }
    }

    void EffekseerRenderer::PlayEffect(System::String^ path, float x, float y, float z)
    {
        if (m_impl)
        {
            std::wstring wpath = msclr::interop::marshal_as<std::wstring>(path);
            m_impl->PlayEffect(wpath, x, y, z);
        }
    }

    void EffekseerRenderer::Destroy()
    {
        if (m_impl)
        {
            m_impl->Shutdown();
        }
    }

    int EffekseerRenderer::GetTotalFrame()
    {
        if (!m_impl) return 0;
        std::wstring key = m_impl->GetLastPlayedKey();
        if (key.empty()) return 0;
        return m_impl->GetTotalFrame(key);
    }
}
