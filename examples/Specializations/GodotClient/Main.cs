using Godot;
using GameRules;
using System.Text.Json;

public partial class Main : Node
{
    private readonly Inventory inventory = new();
    private Label? label;
    private int pickup;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--verify"))
        {
            try
            {
                inventory.PickUp("scene:coin-1", 10);
                var json = JsonSerializer.Serialize(inventory.Snapshot());
                var restored = new Inventory();
                restored.Restore(JsonSerializer.Deserialize<SaveData>(json)!);
                if (restored.PickUp("scene:coin-1", 10) || restored.Coins != 10)
                    throw new InvalidOperationException("Save lost deduplication.");
                var motion = new Motion();
                for (int i = 0; i < 60; i++) motion.Tick(1, 0, 1.0 / 60);
                if (Math.Abs(motion.X - 3) > 1e-9) throw new InvalidOperationException("Delta integration failed.");
                GD.Print("PASS Godot: scene, C# rules, JSON round trip and delta time");
                GetTree().Quit();
            }
            catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
            return;
        }
        var box = new VBoxContainer { Position = new Vector2(30, 30) };
        AddChild(box);
        label = new Label();
        box.AddChild(label);
        var button = new Button { Text = "Pick up 10 coins" };
        box.AddChild(button);
        button.Pressed += () => inventory.PickUp("button:" + pickup++, 10);
        inventory.Changed += OnChanged;
        OnChanged(inventory.Coins);
    }

    private void OnChanged(int coins) { if (label is not null) label.Text = $"Coins: {coins}/{Inventory.Capacity}"; }
    public override void _ExitTree() => inventory.Changed -= OnChanged;
}
