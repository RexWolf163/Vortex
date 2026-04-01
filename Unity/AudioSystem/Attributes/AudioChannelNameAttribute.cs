using System;
using UnityEngine;

namespace Vortex.Unity.AudioSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AudioChannelNameAttribute : PropertyAttribute
    {
        public AudioChannelNameAttribute()
        {
        }
    }
}