using System;
using UnityEngine;
using Vortex.Core.AudioSystem.Model;

namespace Vortex.Unity.AudioSystem.Model
{
    public class SoundWrapper : AudioSampleWrapper
    {
        public SoundWrapper(AudioClip clip, bool loop = false)
        {
            IsLoop = loop;
            Duration = clip.length;
        }

        public override void Dispose() => Stop();

        protected override void Play()
        {
            throw new NotImplementedException();
        }

        protected override void Stop()
        {
            throw new NotImplementedException();
        }

        protected override void Pause()
        {
            throw new NotImplementedException();
        }
    }
}