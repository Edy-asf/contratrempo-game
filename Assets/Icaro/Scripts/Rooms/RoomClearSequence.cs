using System.Collections;
using UnityEngine;

namespace Icaro
{
    // Respiro no último abate de uma sala (RoomManager.RunRoom): o tempo cai para câmera lenta por um instante
    // (SlowScale por SlowTime segundos) e volta numa rampa, a faixa do visor anuncia a arena limpa e todos os
    // cristais de Cronita soltos voam até o jogador (Play). Depois que o domo se desfaz e o multiplicador desce, Collect
    // espera os cristais chegarem (ou CollectTimeout). A física acompanha a câmera lenta (fixedDeltaTime na mesma proporção, devolvido no fim). Com o
    // menu de pausa aberto, ele manda no tempo e o respiro só espera. Jogador morto: termina na hora.
    public static class RoomClearSequence
    {
        const float k_SlowScale = 0.25f;
        const float k_SlowTime = 1.6f;
        const float k_RampTime = 0.9f;
        const float k_CollectTimeout = 1.8f;

        static float s_BaseFixedDelta = -1f;

        public static IEnumerator Play(RoomManager room)
        {
            if (PlayerDead())
                yield break;

            if (s_BaseFixedDelta < 0f)
                s_BaseFixedDelta = Time.fixedDeltaTime;

            bool boss = room && room.IsBoss;
            string arena = room ? room.RoomName : "";
            VisorBanner.Show(boss ? "Chefe derrotado" : "Arena limpa", arena, BannerTone.Success);
            CronitaPickup.AttractAll();

            // Câmera lenta e volta
            float clock = 0f;
            while (clock < k_SlowTime + k_RampTime)
            {
                if (PlayerDead())
                    break;
                if (!MenuOpen())
                {
                    clock += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01((clock - k_SlowTime) / k_RampTime);
                    SetScale(Mathf.Lerp(k_SlowScale, 1f, u * u * (3f - 2f * u)));
                }

                yield return null;
            }

            if (!MenuOpen())
                SetScale(1f);
            Time.fixedDeltaTime = s_BaseFixedDelta;
        }

        // Cristais chegando (os que caíram depois do primeiro chamado também)
        public static IEnumerator Collect()
        {
            float wait = 0f;
            while (wait < k_CollectTimeout && !PlayerDead())
            {
                if (!MenuOpen())
                {
                    wait += Time.unscaledDeltaTime;
                    CronitaPickup.AttractAll();
                    if (CronitaPickup.ActiveCount == 0)
                        break;
                }

                yield return null;
            }
        }

        static void SetScale(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = s_BaseFixedDelta * scale;
        }

        static bool MenuOpen() => GameMenus.Instance && GameMenus.Instance.IsOpen;

        static bool PlayerDead()
        {
            if (!PlayerStats.Instance)
                return true;
            var health = PlayerStats.Instance.GetComponent<Unity.FPS.Game.Health>();
            return health && health.CurrentHealth <= 0f;
        }
    }
}
