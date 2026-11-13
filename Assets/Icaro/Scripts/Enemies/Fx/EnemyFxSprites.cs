using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icaro
{
    // Converte os quadros do EnemyFxArt em Sprites (filtro Point, pivô no meio) e guarda para reaproveitar. O tamanho
    // no mundo vem da densidade de pixels (24 px/m para os efeitos dos inimigos, 12 px/m para os do chão).
    public static class EnemyFxSprites
    {
        static readonly Dictionary<string, Sprite[]> s_Cache = new Dictionary<string, Sprite[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Cache.Clear();

        public static Sprite[] Bolt(EnemyFxStyle style) =>
            Get("Bolt_" + style, () => EnemyFxArt.Bolt(style), EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Ghost(EnemyFxStyle style) =>
            Get("Ghost_" + style, () => EnemyFxArt.Ghost(style), EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Flash(EnemyFxStyle style) =>
            Get("Flash_" + style, () => EnemyFxArt.Flash(style), EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Charge(EnemyFxStyle style) =>
            Get("Charge_" + style, () => EnemyFxArt.Charge(style), EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Impact(EnemyFxStyle style) =>
            Get("Impact_" + style, () => EnemyFxArt.Impact(style), EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Scorch(EnemyFxStyle style) =>
            Get("Scorch_" + style, () => new[] { EnemyFxArt.Scorch(style) }, EnemyFxArt.PixelsPerMeter);

        public static Sprite[] SporePuff() =>
            Get("SporePuff", EnemyFxArt.SporePuff, EnemyFxArt.PixelsPerMeter);

        public static Sprite[] Marker(float radius) =>
            Get("Marker_" + radius.ToString("0.0"), () => EnemyFxArt.Marker(radius), EnemyFxArt.GroundPixelsPerMeter);

        public static Sprite[] Blast(float radius) =>
            Get("Blast_" + radius.ToString("0.0"), () => EnemyFxArt.Blast(radius), EnemyFxArt.GroundPixelsPerMeter);

        static Sprite[] Get(string key, Func<PixelCanvas[]> draw, float pixelsPerMeter)
        {
            if (s_Cache.TryGetValue(key, out Sprite[] cached) && cached[0])
                return cached;

            PixelCanvas[] frames = draw();
            var sprites = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                Texture2D texture = ItemArt.ToTexture(frames[i], key + "_" + i);
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), pixelsPerMeter, 0, SpriteMeshType.FullRect);
                sprites[i].name = key + "_" + i;
            }

            s_Cache[key] = sprites;
            return sprites;
        }

        // Renderer de sprite sem sombra, para os efeitos
        public static SpriteRenderer CreateRenderer(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // Rotação que deixa o sprite de frente para a câmera com o eixo x dele ao longo de "along" (projetado na tela)
        public static Quaternion FacingAlong(Vector3 position, Vector3 along, Camera camera, out float foreshortening)
        {
            Vector3 forward = position - camera.transform.position;
            if (forward.sqrMagnitude < 0.0001f)
                forward = camera.transform.forward;
            forward.Normalize();
            Vector3 onScreen = Vector3.ProjectOnPlane(along, forward);
            float length = along.magnitude;
            foreshortening = length > 0.0001f ? onScreen.magnitude / length : 0f;
            if (onScreen.sqrMagnitude < 0.000001f)
                onScreen = camera.transform.right;
            return Quaternion.LookRotation(forward, Vector3.Cross(forward, onScreen.normalized));
        }
    }
}
