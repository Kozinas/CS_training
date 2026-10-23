// Copy into Assets with the GameRules DLL built for netstandard2.1.
// This adapter requires Unity; it is intentionally outside the .NET solution.
using GameRules;
using UnityEngine;

public sealed class InventoryBehaviour : MonoBehaviour
{
    private readonly Inventory inventory = new Inventory();
    private readonly Motion motion = new Motion();
    private void OnEnable() => inventory.Changed += Show;
    private void OnDisable() => inventory.Changed -= Show;
    private void Show(int coins) => Debug.Log($"Coins: {coins}");
    public void PickUp(string stableSpawnId) => inventory.PickUp(stableSpawnId, 10);
    private void Update()
    {
        // Automatic forward motion avoids a dependency on an input-system package.
        // Clamp stalls in this visual demo; a physics simulation needs a fixed-step policy.
        motion.Tick(1, 0, Mathf.Min(Time.deltaTime, 0.25f));
        transform.position = new Vector3((float)motion.X, (float)motion.Y, 0);
    }
}
