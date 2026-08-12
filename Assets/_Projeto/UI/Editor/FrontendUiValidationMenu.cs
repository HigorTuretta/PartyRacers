using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.UI.Editor
{
    /// <summary>Atalhos de validacao. Nao participa do build nem da UI do jogador.</summary>
    public static class FrontendUiValidationMenu
    {
        const string Menu = "Party Racers/UI Toolkit/Validacao/";

        [MenuItem(Menu + "Game View 1920x1080")]
        public static void SetGameView1920x1080()
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type groupEnum = editor.GetType("UnityEditor.GameViewSizeGroupType");
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object standalone = Enum.Parse(groupEnum, "Standalone");
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(sizes, new[] { standalone });

            Type actualGroupType = group.GetType();
            int builtIn = (int)actualGroupType.GetMethod("GetBuiltinCount").Invoke(group, null);
            int custom = (int)actualGroupType.GetMethod("GetCustomCount").Invoke(group, null);
            int selected = -1;

            for (int i = 0; i < builtIn + custom; i++)
            {
                object size = actualGroupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                int width = (int)sizeType.GetProperty("width").GetValue(size);
                int height = (int)sizeType.GetProperty("height").GetValue(size);
                if (width == 1920 && height == 1080) { selected = i; break; }
            }

            if (selected < 0)
            {
                object fixedResolution = Enum.Parse(sizeKind, "FixedResolution");
                object size = Activator.CreateInstance(sizeType, fixedResolution, 1920, 1080, "UI Port 1920x1080");
                actualGroupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = builtIn + custom;
            }

            Type gameViewType = editor.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selectedSize = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            selectedSize.SetValue(gameView, selected);
            gameView.Show();
            gameView.Repaint();
            Debug.Log("[UI Toolkit] Game View fixada em 1920x1080.");
        }

        [MenuItem(Menu + "Lobby")]
        public static void Lobby() => Go(ScreenId.Lobby);

        [MenuItem(Menu + "Garagem")]
        public static void Garage() => Go(ScreenId.Garage);

        [MenuItem(Menu + "Sala Privada")]
        public static void Custom() => Go(ScreenId.CustomMatch);

        [MenuItem(Menu + "Busca")]
        public static void Matchmaking() => Go(ScreenId.Matchmaking);

        static void Go(ScreenId screen)
        {
            FrontendRouter router = UnityEngine.Object.FindAnyObjectByType<FrontendRouter>(FindObjectsInactive.Include);
            if (router == null) throw new InvalidOperationException("FrontendRouter nao encontrado.");
            router.Go(screen);
        }
    }
}
