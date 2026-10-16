using System;
using UnityEngine;

namespace Icaro
{
    // Carteira da run: Cronitas (moeda das lojas) e fichas de rerrolagem da escolha de itens. Como os itens, dura a
    // run e some ao morrer (a cena é recarregada). O PlayerStats adiciona este componente ao jogador.
    public class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        public int Cronitas { get; private set; }
        public int RerollTokens { get; private set; }

        // Melhorias compradas na loja nesta run (o preço sobe a cada uma)
        public int DamageUpgrades { get; private set; }
        public int FireRateUpgrades { get; private set; }

        // Mudou algo (para o HUD)
        public event Action OnChanged;

        // Recolheu Cronitas (quanto), para o HUD animar
        public event Action<int> OnCollected;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
                return;
            Cronitas += amount;
            OnCollected?.Invoke(amount);
            OnChanged?.Invoke();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Cronitas < amount)
                return false;
            Cronitas -= amount;
            OnChanged?.Invoke();
            return true;
        }

        public void AddRerollToken()
        {
            RerollTokens++;
            OnChanged?.Invoke();
        }

        public bool UseRerollToken()
        {
            if (RerollTokens <= 0)
                return false;
            RerollTokens--;
            OnChanged?.Invoke();
            return true;
        }

        public void AddDamageUpgrade()
        {
            DamageUpgrades++;
            OnChanged?.Invoke();
        }

        public void AddFireRateUpgrade()
        {
            FireRateUpgrades++;
            OnChanged?.Invoke();
        }
    }
}
