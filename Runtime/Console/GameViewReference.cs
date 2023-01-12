// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using System;
using System.Reflection;
using UnityEngine;

namespace AggroBird.DebugConsole
{
    internal sealed class GameViewReference
    {
        public GameViewReference()
        {
            try
            {
                gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor", false);
                if (gameViewType != null)
                {
                    positionInfo = gameViewType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
            catch (Exception)
            {

            }
        }

        public float GetGameViewScale()
        {
            if (positionInfo != null)
            {
                try
                {
                    bool hasGameView = gameViewWindow;

                    if (!hasGameView)
                    {
                        foreach (var window in Resources.FindObjectsOfTypeAll(gameViewType))
                        {
                            if (window is ScriptableObject scriptableObject)
                            {
                                gameViewWindow = scriptableObject;
                                hasGameView = true;
                            }
                        }
                    }

                    if (hasGameView)
                    {
                        Rect gameViewRect = (Rect)positionInfo.GetValue(gameViewWindow);
                        float vw = gameViewRect.width;
                        float vh = gameViewRect.height - 17;
                        float sw = Screen.width;
                        float sh = Screen.height;
                        float va = Mathf.Max(vh, 1) / Mathf.Max(vw, 1);
                        float sa = Mathf.Max(sh, 1) / Mathf.Max(sw, 1);
                        return (va > sa) ? (sw / vw) : (sh / vh);
                    }
                }
                catch (Exception)
                {

                }
            }

            return 1;
        }

        private Type gameViewType;
        private PropertyInfo positionInfo;
        private ScriptableObject gameViewWindow;
    }
}
#endif