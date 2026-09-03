using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    /// <summary>
    /// Режим подключения/отключения слоев
    /// </summary>
    [Serializable]
    public class GameObjectsSwitch : StateItem
    {
        [SerializeField] private GameObject[] links = { };

        /// <summary>
        /// Задержка на кадр при включении
        /// </summary>
        [SerializeField] private bool onDelayed = false;

        /// <summary>
        /// Задержка на кадр при выключении
        /// Защищает от мерцания при переключении свитчера
        /// </summary>
        [InfoBox("Задержка на кадр при выключении\nЗащищает от мерцания при переключении свитчера")] [SerializeField]
        private bool offDelayed = false;

        public override void Set()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SwitchOn();
                return;
            }
#endif
            if (onDelayed)
                foreach (var gameObject in links)
                {
                    if (gameObject != null)
                        TimeController.Call(() =>
                        {
                            if (gameObject != null) gameObject.SetActive(true);
                        }, gameObject.transform);
                }
            else
                SwitchOn();
        }

        public override void DefaultState()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SwitchOff();
                return;
            }
#endif
            if (offDelayed)
                foreach (var gameObject in links)
                {
                    if (gameObject != null)
                        TimeController.Call(() =>
                        {
                            if (gameObject != null) gameObject.SetActive(false);
                        }, gameObject.transform);
                }
            else
                SwitchOff();
        }

        private void SwitchOn()
        {
            foreach (var gameObject in links)
            {
                if (gameObject != null)
                {
                    TimeController.RemoveCall(gameObject.transform);
                    gameObject.SetActive(true);
                }
            }
        }

        private void SwitchOff()
        {
            foreach (var gameObject in links)
            {
                if (gameObject != null)
                {
                    TimeController.RemoveCall(gameObject.transform);
                    gameObject.SetActive(false);
                }
#if UNITY_EDITOR
                else if (!Application.isPlaying)
                {
                    Debug.LogError("[GameObjectSwitch] Has null link!");
                }
#endif
            }
        }


        public override void Dispose()
        {
            foreach (var gameObject in links)
            {
                if (gameObject != null)
                {
                    TimeController.RemoveCall(gameObject.transform);
                    gameObject.SetActive(false);
                }
            }

            base.Dispose();
        }

        public override bool IsValid()
        {
            foreach (var link in links)
            {
                if (link == null)
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR
        public override StateItem Clone()
        {
            var temp = new List<GameObject>();
            temp.AddRange(links);
            var clone = new GameObjectsSwitch
            {
                links = temp.ToArray(),
                onDelayed = onDelayed,
                offDelayed = offDelayed,
            };
            return clone;
        }

        public override string DropDownGroupName => "Objects";
        public override string DropDownItemName => "GameObjects Switch";

#endif
    }
}