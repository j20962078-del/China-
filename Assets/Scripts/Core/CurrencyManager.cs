using UnityEngine;

namespace ChinaFishing
{
    // Единая точка работы с валютой: серебро (SaveData.money) и золото (SaveData.gold).
    // Любое изменение сохраняется и оповещает интерфейс через Game.Changed.
    public static class CurrencyManager
    {
        public static int Silver { get { return Game.S != null ? Game.S.money : 0; } }
        public static int Gold { get { return Game.S != null ? Game.S.gold : 0; } }

        public static bool CanAfford(int silver, int gold)
        {
            return Game.S != null && Game.S.money >= silver && Game.S.gold >= gold;
        }

        // Списывает обе валюты сразу или не списывает ничего
        public static bool TrySpend(int silver, int gold)
        {
            if (silver < 0 || gold < 0 || !CanAfford(silver, gold)) return false;
            Game.S.money -= silver; Game.S.gold -= gold;
            return true;
        }

        public static void AddSilver(int v)
        {
            if (v <= 0 || Game.S == null) return;
            Game.S.money = (int)System.Math.Min((long)Game.S.money + v, 2000000000L);
        }

        public static void AddGold(int v)
        {
            if (v <= 0 || Game.S == null) return;
            Game.S.gold = (int)System.Math.Min((long)Game.S.gold + v, 2000000000L);
        }
    }
}
