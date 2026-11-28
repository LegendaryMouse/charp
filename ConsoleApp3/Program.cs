using System.Text.Json;
using Spectre.Console;

int delayMs = 250;

// Проверка аргументов
if (args.Length == 0)
{
    Console.WriteLine("No arguments passed");
}
else
{
    if (args.Contains("--fast"))
    {
        delayMs = 100;
        Console.WriteLine("Fast battle mode enabled");
    }
}
Console.WriteLine();


List<FighterConfig> fightersConfigList = new List<FighterConfig>();
fightersConfigList.Add(new FighterConfig(150, 5, 2, 0.2f, "White Wolf"));
fightersConfigList.Add(new FighterConfig(100, 3, 3, 0.5f, "Black Wolf"));
fightersConfigList.Add(new FighterConfig(50, 3, 3, 0.2f, "Black Wolf Brother"));

using var logger = new StreamWriter("fighters_list.txt", append: false);
var options = new JsonSerializerOptions { WriteIndented = true };
logger.Write(JsonSerializer.Serialize(fightersConfigList, options));
logger.Close();

// Десериализация
static List<FighterConfig> DeserializeFightersList()
{
    using var logger = new StreamReader("fighters_list.txt");
    List<FighterConfig> fighters = JsonSerializer.Deserialize<List<FighterConfig>>(logger.BaseStream);
    AnsiConsole.MarkupLine("[yellow]Fighters loaded from JSON successfully![/]");
    return fighters;
}
var fighters = DeserializeFightersList().Select(c => new Fighter(c.HitPoints, c.Damage, c.Defense, c.CritChance, c.Name)).ToList();

await Arena.Test(Arena.BattleRoyale, fighters, 1, delayMs);

public static class Arena
{
    public static async Task BattleRoyale(List<Fighter> fighters, int delay)
    {
        // Вступление: заголовок, представление и запись всех бойцов в очередь
        using var logger = new StreamWriter("battle_log.txt", append: true);
        AnsiConsole.MarkupLine("[bold red]THE ROYAL BATTLE HAS BEGUN[/]");
        Console.WriteLine("PARTICIPANTS: ");
        await logger.WriteLineAsync("THE ROYAL BATTLE HAS BEGUN");

        foreach (Fighter fighter in fighters)
        {
            Console.WriteLine($"{fighter.Name}");
        }
        await Task.Delay(3000);
        Console.Clear();

        Queue<IDamageable> fightersQueue = new Queue<IDamageable>();
        foreach (IDamageable figher in fighters)
        {
            fightersQueue.Enqueue(figher);
            figher.OnDied += OnFighterDeath;
        }

        // Основной цикл боя
        while (fightersQueue.Count(f => f is Fighter && f.IsAlive) > 1)
        {
            for (int i = 0; i < fightersQueue.Count; i++)
            {
                IDamageable fighter = fightersQueue.Dequeue();

                if (!fighter.IsAlive) { continue; }
                if (fighter is not Fighter attacker) { fightersQueue.Enqueue(fighter); continue; }

                List<IDamageable> targets = fightersQueue.Where(p => p.IsAlive && p != attacker).ToList();
                if (targets.Count == 0) { fightersQueue.Enqueue(fighter); break; }

                IDamageable target = targets[Random.Shared.Next(0, targets.Count)];
                await attacker.Attack(target);
                await logger.WriteLineAsync($"{attacker.Name} attacks {target.Name}");

                fightersQueue.Enqueue(fighter);
            }

            // Шкалы здоровья
            Console.WriteLine();
            foreach (IDamageable fighter in  fightersQueue)
            {
                DrawHealthBar(fighter);
            }

            await Task.Delay(delay);
            Console.Clear();
        }

        // Финал: титры, имя победителя или ничья
        Console.WriteLine("THE BATTLE IS OVER");
        await logger.WriteLineAsync("THE BATTLE IS OVER");
        var winner = fightersQueue.FirstOrDefault(p => p.IsAlive && p is Fighter);
        if (winner != null) { Console.WriteLine($"{winner.Name.ToUpper()} WON!"); winner.OnDied -= OnFighterDeath; }
        else Console.WriteLine("EVERYONE'S DEAD. DRAW!)");
    }
    public static async Task Test(Func<List<Fighter>, int, Task> func, List<Fighter> list, int count, int delay)
    {
        for (int i = 0;i < count;i++)
        {
            await func(list, delay);
        }
    }
    public static void OnFighterDeath(IDamageable corpse)
    {
        Console.WriteLine(corpse.Name + " Died!");
    }
    public static void DrawHealthBar(IDamageable fighter)
    {
        double percentage = (double)fighter.HitPoints / (double)fighter.MaxHP;
        Console.Write(fighter.Name + ": ");
        Console.Write("[");
        for (int i = 0; i < 10; i++)
        {
            if (percentage * 10 > i)
            {
                Console.Write("█");
            }
            else
            {
                Console.Write("░");
            }
        }
        Console.Write("]");
        Console.WriteLine($" {fighter.HitPoints} / {fighter.MaxHP}");
    }
}
public readonly struct DamageInfo
{
    public int Damage { get; }
    public bool IsCritical { get; }
    public DamageInfo(int damage, bool isCritical)
    {
        Damage = damage;
        IsCritical = isCritical;
    }
}
public interface IDamageable : IDisposable
{
    int HitPoints { get; }
    int MaxHP { get; }
    bool IsAlive { get; }
    string Name { get; }

    Task TakeDamage(DamageInfo damageInfo);

    event Action<IDamageable> OnDied;
}
public class FighterConfig
{
    public FighterConfig(int hitPoints, int damage, int defense, float critChance, string name)
    {
        Name = name;
        HitPoints = hitPoints;
        Damage = damage;
        Defense = defense;
        CritChance = critChance;
    }

    public string Name { get; set; }
    public int HitPoints { get; set; }
    public int Damage { get; set; }
    public int Defense { get; set; }
    public float CritChance { get; set; }
}
public class Fighter : IDamageable
{
    private int _hitPoints;
    public int HitPoints
    {
        get
        {
            return _hitPoints;
        }
        set
        {
            if (value < 0) { _hitPoints = 0; }
            else { _hitPoints = value; }
        }
    }

    public int MaxHP { get; private set; }
    private int _damage { get; set; }
    private int _defense { get; set; }
    private float _critChance { get; set; }
    public string Name { get; private set; }
    public bool IsAlive => HitPoints > 0;

    public override bool Equals(object? obj)
    {
        if (obj is not Fighter other) return false;
        return this.Name == other.Name;
    }
    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
    public async Task Attack(IDamageable target)
    {
        int damage = _damage;
        bool isCritical = _critChance * 100 > Random.Shared.Next(0, 100);
        DamageInfo damageInfo = new DamageInfo(damage, isCritical);
        await target.TakeDamage(damageInfo);

        if (isCritical) Console.Write("Critical hit! ");
        Console.WriteLine($"{this.Name} attacks {target.Name} with {damage} damage");
    }
    public virtual async Task TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.IsCritical) HitPoints -= (damageInfo.Damage * 2 - this._defense);
        else HitPoints -= (damageInfo.Damage - this._defense);

        if (!IsAlive) { OnDied?.Invoke(this); Dispose(); }
    }
    public event Action<IDamageable> OnDied;
    public void Dispose()
    {
        OnDied = null;
    }
    public Fighter(int hitPoints, int damage, int defense, float critChance, string name)
    {
        this.HitPoints = hitPoints;
        this.MaxHP = hitPoints;
        this._damage = damage;
        this._defense = defense;
        this._critChance = critChance;
        this.Name = name;
    }
}
public class Barrel :IDamageable
{
    private int _hitPoints;
    public int HitPoints
    {
        get
        {
            return _hitPoints;
        }
        set
        {
            if (value < 0) { _hitPoints = 0; }
            else { _hitPoints = value; }
        }
    }

    public int MaxHP { get; private set; }
    private int _defense { get; set; }
    public string Name { get; private set; }
    public bool IsAlive => HitPoints > 0;
    public virtual async Task TakeDamage(DamageInfo damageInfo)
    {
        if (damageInfo.IsCritical) HitPoints -= (damageInfo.Damage * 2 - this._defense);
        else HitPoints -= (damageInfo.Damage - this._defense);

        if (!IsAlive) { OnDied?.Invoke(this); Dispose(); }
    }
    public event Action<IDamageable> OnDied;
    public void Dispose()
    {
        OnDied = null;
    }
    public Barrel(int hitPoints, int defense, string name)
    {
        HitPoints = hitPoints;
        MaxHP = hitPoints;
        _defense = defense;
        Name = name;
    }
}
public class Rogue : Fighter
{
    private float _dodgeChange;

    public override async Task TakeDamage(DamageInfo damageInfo)
    {
        if (_dodgeChange * 100 > Random.Shared.Next(0, 100))
        {
            return;
        }
        await base.TakeDamage(damageInfo);
    }

    public Rogue(int hitPoint, int damage, int defense, float critChance, string name, float dodgeChange)
        : base(hitPoint, damage, defense, critChance, name)
    {
        this._dodgeChange = dodgeChange;
    }
}