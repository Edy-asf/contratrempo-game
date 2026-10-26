using System.Threading.Tasks;
using UnityEngine;

namespace Icaro
{
    // Texturas de ruído das nuvens de volume (CloudSea, shaders Icaro/CloudVolume e Icaro/CloudWisp), geradas em jogo
    // uma vez e guardadas: um volume 3D de 64³ que se repete (R: Perlin-Worley das formas grandes; G e B: Worley em duas
    // escalas menores, para corroer as bordas em fiapos) e um tufo de fumaça 2D (A: densidade com a borda em fiapos;
    // G: o lado de cima mais claro, como se a luz viesse de cima).
    public static class CloudNoise
    {
        const int k_Size = 64;
        const int k_PuffSize = 128;

        static Texture3D s_Volume;
        static Texture2D s_Puff;

        public static Texture3D Volume
        {
            get
            {
                if (!s_Volume)
                    s_Volume = BuildVolume();
                return s_Volume;
            }
        }

        public static Texture2D Puff
        {
            get
            {
                if (!s_Puff)
                    s_Puff = BuildPuff();
                return s_Puff;
            }
        }

        static Texture3D BuildVolume()
        {
            var colors = new Color32[k_Size * k_Size * k_Size];
            Parallel.For(0, k_Size, z =>
            {
                for (int y = 0; y < k_Size; y++)
                for (int x = 0; x < k_Size; x++)
                {
                    var p = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) / k_Size;
                    float perlin = ValueFbm(p, 4, 4, 101);
                    float worley = 1f - Worley(p, 4, 7);
                    float baseShape = Mathf.Clamp01(Remap(perlin, 0f, 1f, worley * 0.6f - 0.15f, 1f));
                    float mid = (1f - Worley(p, 8, 11)) * 0.625f + (1f - Worley(p, 16, 13)) * 0.25f +
                                (1f - Worley(p, 32, 17)) * 0.125f;
                    float fine = (1f - Worley(p, 16, 19)) * 0.625f + (1f - Worley(p, 32, 23)) * 0.375f;
                    colors[x + y * k_Size + z * k_Size * k_Size] = new Color32(Byte(baseShape), Byte(mid), Byte(fine), 255);
                }
            });

            var texture = new Texture3D(k_Size, k_Size, k_Size, TextureFormat.RGBA32, false)
            {
                name = "Nuvem_Volume",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D BuildPuff()
        {
            var colors = new Color32[k_PuffSize * k_PuffSize];
            for (int y = 0; y < k_PuffSize; y++)
            for (int x = 0; x < k_PuffSize; x++)
            {
                var uv = new Vector2(x + 0.5f, y + 0.5f) / k_PuffSize;
                Vector2 c = uv * 2f - Vector2.one;
                float noise = ValueFbm(new Vector3(uv.x, uv.y, 0.37f), 5, 5, 211);
                float edge = 1f - c.magnitude * (0.75f + noise * 0.5f);
                float density = Mathf.Clamp01(edge * 1.6f) * Mathf.Clamp01(noise * 1.3f + edge * 0.6f);
                density = Mathf.SmoothStep(0f, 1f, density);
                float lit = Mathf.Clamp01(0.5f + c.y * 0.45f + (noise - 0.5f) * 0.6f);
                colors[x + y * k_PuffSize] = new Color32(255, Byte(lit), 255, Byte(density));
            }

            var texture = new Texture2D(k_PuffSize, k_PuffSize, TextureFormat.RGBA32, true)
            {
                name = "Nuvem_Tufo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(colors);
            texture.Apply(true, true);
            return texture;
        }

        static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        static float Remap(float v, float a, float b, float c, float d) => c + (v - a) / (b - a) * (d - c);

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Wrap(int v, int period) => ((v % period) + period) % period;

        // Ruído de valor suave que se repete a cada 'period' células, em oitavas (cada uma com o dobro do período)
        static float ValueFbm(Vector3 p, int period, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(p * period, period, seed + o * 31) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        static float Value(Vector3 p, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x0), fy = Smooth(p.y - y0), fz = Smooth(p.z - z0);
            float result = 0f;
            for (int k = 0; k < 8; k++)
            {
                int dx = k & 1, dy = (k >> 1) & 1, dz = (k >> 2) & 1;
                float w = (dx == 1 ? fx : 1f - fx) * (dy == 1 ? fy : 1f - fy) * (dz == 1 ? fz : 1f - fz);
                result += w * Hash(Wrap(x0 + dx, period), Wrap(y0 + dy, period), Wrap(z0 + dz, period), seed);
            }

            return result;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        // Distância até o ponto mais perto (Worley), com um ponto por célula e a grade se repetindo
        static float Worley(Vector3 p, int cells, int seed)
        {
            Vector3 q = p * cells;
            int cx = Mathf.FloorToInt(q.x), cy = Mathf.FloorToInt(q.y), cz = Mathf.FloorToInt(q.z);
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy, z = cz + dz;
                int wx = Wrap(x, cells), wy = Wrap(y, cells), wz = Wrap(z, cells);
                var point = new Vector3(x + Hash(wx, wy, wz, seed), y + Hash(wx, wy, wz, seed + 1),
                    z + Hash(wx, wy, wz, seed + 2));
                best = Mathf.Min(best, (point - q).sqrMagnitude);
            }

            return Mathf.Clamp01(Mathf.Sqrt(best));
        }
    }
}
