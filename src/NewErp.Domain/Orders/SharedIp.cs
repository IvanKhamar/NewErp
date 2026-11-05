namespace NewErp.Domain.Orders;

/// <summary>Різні клієнти замовляли з однієї IP-адреси («аналіз IP» у перевірці на шахрайство на старому сайті).</summary>
public sealed record SharedIp(string Ip, int UniqueClients, IReadOnlyList<string> Clients)
{
    /// <summary>Від стількох різних клієнтів на одній адресі варто придивитися.</summary>
    public const int MinClients = 3;

    /// <summary>Скільки клієнтів показуємо в попередженні.</summary>
    public const int Listed = 10;

    /// <summary>Клієнтів розрізняємо за іменем і цифрами телефону, як групував старий сайт.</summary>
    public static SharedIp? From(string ip, IEnumerable<(string Firstname, string Lastname, string Telephone)> orders)
    {
        var clients = orders
            .Select(o => (Name: $"{o.Firstname.Trim()} {o.Lastname.Trim()}".Trim(), o.Telephone))
            .DistinctBy(c => (c.Name.ToLowerInvariant(), new string(c.Telephone.Where(char.IsAsciiDigit).ToArray())))
            .Select(c => $"{c.Name} ({c.Telephone.Trim()})")
            .ToList();
        return clients.Count < MinClients ? null : new SharedIp(ip, clients.Count, clients.Take(Listed).ToList());
    }
}
