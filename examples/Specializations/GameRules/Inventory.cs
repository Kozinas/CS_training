using System;
using System.Collections.Generic;
using System.Linq;

namespace GameRules
{
    public sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public int Coins { get; set; }
        public string[] Pickups { get; set; } = Array.Empty<string>();
    }

    // Plain rules with no Node, MonoBehaviour, engine serializer or UI lifetime.
    public sealed class Inventory
    {
        public const int Capacity = 100;
        private HashSet<string> pickups = new HashSet<string>(StringComparer.Ordinal);
        public int Coins { get; private set; }
        public event Action<int>? Changed;

        public bool PickUp(string spawnId, int amount)
        {
            if (string.IsNullOrWhiteSpace(spawnId) || spawnId.Length > 64 || amount < 1 || amount > Capacity)
                throw new ArgumentException("Invalid pickup.");
            if (pickups.Contains(spawnId)) return false;
            if (Coins > Capacity - amount || pickups.Count >= 1000) return false;
            pickups.Add(spawnId);
            Coins += amount;
            Changed?.Invoke(Coins);
            return true;
        }

        public bool Spend(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Coins < amount) return false;
            Coins -= amount;
            Changed?.Invoke(Coins);
            return true;
        }

        public SaveData Snapshot() => new SaveData
        { Coins = Coins, Pickups = pickups.OrderBy(x => x, StringComparer.Ordinal).ToArray() };

        public void Restore(SaveData save)
        {
            if (save is null || save.Version != 1 || save.Coins < 0 || save.Coins > Capacity
                || save.Pickups is null || save.Pickups.Length > 1000)
                throw new ArgumentException("Invalid save.");
            var candidate = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in save.Pickups)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !candidate.Add(id))
                    throw new ArgumentException("Invalid pickup history.");
            // Validate the entire snapshot before changing the live state.
            pickups = candidate;
            Coins = save.Coins;
            Changed?.Invoke(Coins);
        }
    }

    public sealed class Motion
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public void Tick(double axisX, double axisY, double delta)
        {
            if (!double.IsFinite(axisX) || !double.IsFinite(axisY) || !double.IsFinite(delta)
                || Math.Abs(axisX) > 1 || Math.Abs(axisY) > 1 || delta < 0 || delta > 0.25)
                throw new ArgumentOutOfRangeException(nameof(delta));
            double length = Math.Sqrt(axisX * axisX + axisY * axisY);
            double scale = 3 * delta / Math.Max(1, length);
            X += axisX * scale;
            Y += axisY * scale;
        }
    }
}
