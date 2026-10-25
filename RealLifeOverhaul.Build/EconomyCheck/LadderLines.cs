using System;
using System.Collections.Generic;

// The management ladder's line in What's new, as each language has it (LadderChecks.cs, LadderTexts).
internal static partial class Program
{
    private static readonly Dictionary<string, string> LadderLines = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["en"] = "A management ladder at the city's companies",
        ["pt"] = "Uma escada da gestão nas empresas da cidade",
        ["zh-cn"] = "城中公司的管理晋升阶梯",
        ["de"] = "Eine Management-Karriereleiter in den Unternehmen der Stadt",
        ["fr"] = "Une échelle de management dans les entreprises de la ville",
        ["es"] = "Una escalera de gestión en las empresas de la ciudad",
        ["ru"] = "Карьерная лестница управления в компаниях города",
        ["nl"] = "Een managementladder bij de bedrijven van de stad",
        ["tr"] = "Şehrin şirketlerinde bir yönetim merdiveni",
    };
}
