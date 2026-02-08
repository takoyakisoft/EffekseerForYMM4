#pragma once

#include <d3d11.h>

class EffectsManager;

using namespace System;

namespace EffekseerForNative {

        public ref class EffekseerRenderer
        {
        public:
            EffekseerRenderer();
            ~EffekseerRenderer();
            !EffekseerRenderer();

            bool Initialize(IntPtr device, IntPtr context, int width, int height);
            bool LoadEffect(System::String^ path);
            void Render();
            void Update(float deltaFrames);
            void SetSoundCallback(System::IntPtr loadSound, System::IntPtr unloadSound, System::IntPtr playSound);
            void SetProjection(int width, int height);
            void SetProjectionPerspective(float fov, int width, int height, float nearVal, float farVal);
            void SetProjectionOrthographic(float width, float height, float nearVal, float farVal);
            void SetCameraLookAt(float posX, float posY, float posZ, float targetX, float targetY, float targetZ, float upX, float upY, float upZ);
            void SetScale(float scale);
            void Reset();
            void StopRoot();
            void PlayEffect(System::String^ path, float x, float y, float z);
            void Destroy();
            int GetTotalFrame();

        private:
            EffectsManager* m_impl = nullptr;
        };
}