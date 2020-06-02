// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    public class VarjoEvents : MonoBehaviour
    {
        void OnEnable()
        {
            // Listen visibility changes in this application
            // and call this event when visibility changes
            VarjoManager.OnVisibilityEvent += VisibleEvent;

            VarjoManager.OnForegroundEvent += ForegroundEvent;
        }

        void OnDisable()
        {
            VarjoManager.OnVisibilityEvent -= VisibleEvent;
            VarjoManager.OnForegroundEvent -= ForegroundEvent;
        }

        void Update()
        {
            // Called once when headset application button is pressed down
            if (VarjoManager.Instance.GetButtonDown())
            {
                Debug.Log("Application button pressed");
            }

            // Called once when headset application button is released
            if (VarjoManager.Instance.GetButtonUp())
            {
                Debug.Log("Application button released");
            }
        }

        public void VisibleEvent(bool visible)
        {
            if (visible)
            {
                Debug.Log("Application visible");
            }
            else
            {
                Debug.Log("Application hidden");
            }
        }

        public void ForegroundEvent(bool onForeground)
        {
            if (onForeground)
            {
                Debug.Log("Foreground");
            }
            else
            {
                Debug.Log("Background");
            }
        }
    }
}
