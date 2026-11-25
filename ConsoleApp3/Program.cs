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

Dictionary<string, IDamageable> fighters = new Dictionary<string, IDamageable>();


Fighter whiteWolf = new Fighter(150, 5, 2, 0.2f, "White Wolf");
Rogue blackWolf = new Rogue(100, 3, 3, 0.5f, "Black Wolf", 0.2f);
Rogue blackWolf1 = new Rogue(50, 3, 3, 0.2f, "Black Wolf Brother", 0.1f);
Barrel justABarrel = new Barrel(200, 1, "Just Barrel");
Barrel justABarrel1 = new Barrel(250, 3, "Just Another Barrel");
Fighter secretBoss = new Fighter(500, 6, 3, 0.4f, "Secret Boss");

fighters.Add(whiteWolf.Name, whiteWolf);
fighters.Add(blackWolf.Name, blackWolf);
fighters.Add(blackWolf1.Name, blackWolf1);
fighters.Add(justABarrel.Name, justABarrel);
fighters.Add(justABarrel1.Name, justABarrel1);
fighters.Add(secretBoss.Name, secretBoss);

// Проверка Equals
Console.WriteLine(whiteWolf.Equals(whiteWolf));
Console.WriteLine(whiteWolf.Equals(blackWolf));
Console.WriteLine();

// Проверка словаря
if (!fighters.TryGetValue("Secret Boss", out var boss)) { Console.WriteLine("No secret boss :( "); }
else { Console.WriteLine($"THERE WAS A {boss.Name} INDEED!"); }
Console.WriteLine();

// Проверка Span
string rawCommand = "HEAL:White Wolf:50";
Arena.ParseAndExecute(rawCommand);


await Arena.Test(Arena.BattleRoyale, fighters, 1, delayMs);

public static class Arena
{
    public static async Task BattleRoyale(Dictionary<string, IDamageable> fighters, int delay)
    {
        // Вступление: заголовок, представление и запись всех бойцов в очередь
        using var logger = new StreamWriter("battle_log.txt", append: true);
        Console.WriteLine("THE ROYAL BATTLE HAS BEGUN. PARTICIPANTS: ");
        await logger.WriteLineAsync("THE ROYAL BATTLE HAS BEGUN");

        foreach (string name in fighters.Keys)
        {
            Console.WriteLine($"{name}");
        }
        await Task.Delay(3000);
        Console.Clear();

        Queue<IDamageable> fightersQueue = new Queue<IDamageable>();
        foreach (IDamageable figher in fighters.Values)
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
    public static async Task Test(Func<Dictionary<string, IDamageable>, int, Task> func, Dictionary<string, IDamageable> list, int count, int delay)
    {
        for (int i = 0;i < count;i++)
        {
            await func(list, delay);
        }
    }
    public static void ParseAndExecute(string rawCommand)
    {
        ReadOnlySpan<char> span = rawCommand.AsSpan();
        int colonPosition = span.IndexOf(":");
        int lastColonPosition = span.LastIndexOf(":");

        ReadOnlySpan<char> command = span.Slice(0, colonPosition);
        ReadOnlySpan<char> target = span[(colonPosition+1)..lastColonPosition];
        ReadOnlySpan<char> value = span[(lastColonPosition+1)..];

        Console.WriteLine($"Command: {command} Target: {target} Value: {value}");
        Console.WriteLine();
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